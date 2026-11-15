using AwesomeAssertions;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Services;
using MoneyRecord.Services.Repositories;
using MoneyRecord.Services.Sync;
using MoneyRecord.Services.Sync.Lan;
using Xunit;

namespace MoneyRecord.Tests.Services.Sync;

/// <summary>Stage 26.4c-2: receipt files travel as sealed, content-named blobs through the local store, a shared folder and the LAN link.</summary>
public sealed class SyncBlobsTests : IAsyncLifetime
{
    private static readonly string[] Loopback = ["127.0.0.1"];
    private readonly byte[] _key = SyncCrypto.NewKey();
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"moneyrecord-blobs-{Guid.NewGuid():N}");
    private DatabaseInitializer _database = null!;

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(_folder);
        _database = new DatabaseInitializer(new AppPaths(_folder));
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await (await _database.GetConnectionAsync()).CloseAsync();
        Directory.Delete(_folder, recursive: true);
    }

    private async Task<string> AttachAsync(string name, byte[] bytes, long? size = null)
    {
        var file = Path.Combine(_folder, name);
        await File.WriteAllBytesAsync(file, bytes);
        var db = await _database.GetConnectionAsync();
        await db.InsertAsync(new TransactionAttachment { TransactionId = 1, Path = file });
        await AttachmentHashes.FillAsync(_database);
        if (size != null)
            await db.ExecuteAsync("UPDATE \"TransactionAttachment\" SET SizeBytes = ? WHERE Path = ?", size, file);
        return SyncBlobs.HashOf(bytes);
    }

    [Fact]
    public void ASealedBlob_OpensOnlyWithItsKeyItsNameAndItsContent()
    {
        var plain = new byte[] { 9, 8, 7, 6 };
        var hash = SyncBlobs.HashOf(plain);
        var sealedBlob = SyncBlobs.Seal(_key, hash, plain);

        SyncBlobs.Open(_key, hash, sealedBlob).Should().Equal(plain);
        SyncBlobs.Open(SyncCrypto.NewKey(), hash, sealedBlob).Should().BeNull();
        SyncBlobs.Open(_key, SyncBlobs.HashOf([1]), sealedBlob).Should().BeNull(); // stored under another name
        sealedBlob[^1] ^= 1;
        SyncBlobs.Open(_key, hash, sealedBlob).Should().BeNull();

        // Sealed under the right name but not the file the name says
        var wrong = SyncBlobs.Seal(_key, hash, [1, 2, 3]);
        SyncBlobs.Open(_key, hash, wrong).Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("../../etc/passwd")]
    [InlineData("ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789")]
    public async Task OnlyALowercaseSha256_IsABlobName(string name)
    {
        SyncBlobs.IsHash(name).Should().BeFalse();
        var store = new DirectorySyncStore(_folder);
        await Assert.ThrowsAsync<ArgumentException>(() => store.HasAsync(name));
    }

    [Fact]
    public async Task TheBlobFolder_IsNotTakenForADevice()
    {
        var store = new DirectorySyncStore(_folder);
        await store.WriteBlobAsync(SyncBlobs.HashOf([1]), [1]);
        await store.WriteAsync("device1", 1, [1]);

        (await store.DevicesAsync()).Should().Contain("device1").And.NotContain("_blobs");
    }

    [Fact]
    public async Task Upload_SendsEachFileOnce_ToEveryStoreThatLacksIt()
    {
        var bytes = new byte[] { 1, 2, 3, 4 };
        var hash = await AttachAsync("a.jpg", bytes);
        await AttachAsync("copy.jpg", bytes); // the same file attached to another transaction
        var first = new DirectorySyncStore(Path.Combine(_folder, "first"));
        var second = new DirectorySyncStore(Path.Combine(_folder, "second"));
        await second.WriteBlobAsync(hash, SyncBlobs.Seal(_key, hash, bytes));

        (await SyncBlobs.UploadAsync(_database, _key, [first, second])).Should().Be(1);
        (await SyncBlobs.UploadAsync(_database, _key, [first, second])).Should().Be(0);

        SyncBlobs.Open(_key, hash, (await first.ReadBlobAsync(hash))!).Should().Equal(bytes);
    }

    [Fact]
    public async Task Upload_LeavesOutLargeMissingAndChangedFiles()
    {
        var large = await AttachAsync("large.jpg", [1], size: SyncBlobs.MaxBytes + 1);
        var gone = await AttachAsync("gone.jpg", [2]);
        File.Delete(Path.Combine(_folder, "gone.jpg"));
        var changed = await AttachAsync("changed.jpg", [3]);
        await File.WriteAllBytesAsync(Path.Combine(_folder, "changed.jpg"), [4]);
        var store = new DirectorySyncStore(Path.Combine(_folder, "target"));

        (await SyncBlobs.UploadAsync(_database, _key, [store])).Should().Be(0);

        foreach (var hash in new[] { large, gone, changed })
            (await store.HasAsync(hash)).Should().BeFalse();
    }

    [Fact]
    public async Task Fetch_PassesOverStoresWithoutTheFileOrWithAnotherOne()
    {
        var bytes = new byte[] { 5, 5, 5 };
        var hash = SyncBlobs.HashOf(bytes);
        var empty = new DirectorySyncStore(Path.Combine(_folder, "empty"));
        var foreign = new DirectorySyncStore(Path.Combine(_folder, "foreign")); // sealed with another key
        await foreign.WriteBlobAsync(hash, SyncBlobs.Seal(SyncCrypto.NewKey(), hash, bytes));
        var good = new DirectorySyncStore(Path.Combine(_folder, "good"));
        await good.WriteBlobAsync(hash, SyncBlobs.Seal(_key, hash, bytes));

        (await SyncBlobs.FetchAsync(_key, hash, [empty, foreign, good])).Should().Equal(bytes);
        (await SyncBlobs.FetchAsync(_key, hash, [empty, foreign])).Should().BeNull();
        (await SyncBlobs.FetchAsync(_key, "nonsense", [good])).Should().BeNull();
    }

    [Fact]
    public async Task OverTheLanLink_AFileGoesUpAndComesBackDown()
    {
        var bytes = new byte[] { 7, 7, 7, 7 };
        var hash = await AttachAsync("lan.jpg", bytes);
        await using var host = await SyncTestDevice.CreateAsync(_key, TimeProvider.System);
        var hostBlobs = new DirectorySyncStore(Path.Combine(_folder, "host"));
        var (certificate, _) = LanCertificate.Create();
        await using var server = new LanSyncServer(certificate, _key, host.DeviceId, "PC", host.Store, host.Engine.CreateSnapshotAsync,
            new LanSyncTests.MemoryRegistry(), blobs: hostBlobs);
        server.Start();
        var (session, _, _) = await LanSyncSession.PairAsync(server.CreatePairingOffer(Loopback, TimeSpan.FromMinutes(5)), "phone", "Phone", default);
        await using var _ = session;

        (await SyncBlobs.UploadAsync(_database, _key, [session])).Should().Be(1);
        (await SyncBlobs.UploadAsync(_database, _key, [session])).Should().Be(0);

        (await hostBlobs.HasAsync(hash)).Should().BeTrue();
        (await SyncBlobs.FetchAsync(_key, hash, [session])).Should().Equal(bytes);
        (await SyncBlobs.FetchAsync(_key, SyncBlobs.HashOf([0]), [session])).Should().BeNull();
    }

    [Fact]
    public async Task TheHost_RefusesBadNamesAndOversizedBlobs()
    {
        await using var host = await SyncTestDevice.CreateAsync(_key, TimeProvider.System);
        var hostBlobs = new DirectorySyncStore(Path.Combine(_folder, "host"));
        var (certificate, _) = LanCertificate.Create();
        await using var server = new LanSyncServer(certificate, _key, host.DeviceId, "PC", host.Store, host.Engine.CreateSnapshotAsync,
            new LanSyncTests.MemoryRegistry(), blobs: hostBlobs);
        server.Start();
        var (session, _, _) = await LanSyncSession.PairAsync(server.CreatePairingOffer(Loopback, TimeSpan.FromMinutes(5)), "phone", "Phone", default);
        await using var _ = session;

        var badName = () => session.WriteBlobAsync("../x", [1]);
        var tooBig = () => session.WriteBlobAsync(SyncBlobs.HashOf([1]), new byte[SyncBlobs.MaxSealedBytes + 1]);

        (await badName.Should().ThrowAsync<LanSyncException>()).Which.Failure.Should().Be(LanFailure.Rejected);
        (await tooBig.Should().ThrowAsync<LanSyncException>()).Which.Failure.Should().Be(LanFailure.Rejected);
    }
}
