using AwesomeAssertions;
using Microsoft.Extensions.Time.Testing;
using MoneyRecord.Models;
using MoneyRecord.Services.Sync;
using MoneyRecord.Services.Sync.Lan;
using Xunit;

namespace MoneyRecord.Tests.Services.Sync;

/// <summary>Stage 26.3a-1: the local-network link over real loopback sockets and TLS, between two real databases.</summary>
public sealed class LanSyncTests : IAsyncLifetime
{
    private static readonly string[] Loopback = ["127.0.0.1"];
    private readonly byte[] _key = SyncCrypto.NewKey();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 14, 12, 0, 0, TimeSpan.Zero));
    private readonly List<IAsyncDisposable> _disposables = [];
    private SyncTestDevice _a = null!;
    private LanSyncServer _server = null!;

    public async Task InitializeAsync()
    {
        _a = await SyncTestDevice.CreateAsync(_key, TimeProvider.System);
        _disposables.Add(_a);
        var (certificate, _) = LanCertificate.Create();
        _server = new LanSyncServer(certificate, _key, _a.DeviceId, "Test PC", _a.Store, _a.Engine.CreateSnapshotAsync, _time);
        _server.Start();
        _disposables.Add(_server);
    }

    public async Task DisposeAsync()
    {
        foreach (var disposable in _disposables.AsEnumerable().Reverse())
            await disposable.DisposeAsync();
    }

    private LanPairingInfo Offer(TimeSpan? validFor = null) => _server.CreatePairingOffer(Loopback, validFor ?? TimeSpan.FromMinutes(5));

    [Fact]
    public void PairingCode_SurvivesBeingTypedAsText_AndBadTextIsRefused()
    {
        var info = Offer();

        LanPairingInfo.TryParse(info.ToText()).Should().BeEquivalentTo(info);
        LanPairingInfo.TryParse("hello").Should().BeNull();
        LanPairingInfo.TryParse(null).Should().BeNull();
        LanPairingInfo.TryParse("moneyrecord-sync:1:%%%").Should().BeNull();
        LanPairingInfo.TryParse(info.ToText()[..^8]).Should().BeNull();
    }

    [Fact]
    public async Task ScanningTheCode_HandsOverTheSyncKey()
    {
        var (session, key) = await LanSyncSession.PairAsync(Offer(), "phone", default);
        await using var _ = session;

        key.Should().Equal(_key);
        session.PeerDeviceId.Should().Be(_a.DeviceId);
    }

    [Fact]
    public async Task ACode_WorksOnce()
    {
        var info = Offer();
        var (first, _) = await LanSyncSession.PairAsync(info, "phone", default);
        await first.DisposeAsync();

        var again = () => LanSyncSession.PairAsync(info, "another-phone", default);

        (await again.Should().ThrowAsync<LanSyncException>()).Which.Failure.Should().Be(LanFailure.Rejected);
    }

    [Fact]
    public async Task AnExpiredCode_IsRefused()
    {
        var info = Offer(TimeSpan.FromMinutes(5));
        _time.Advance(TimeSpan.FromMinutes(6));

        var act = () => LanSyncSession.PairAsync(info, "phone", default);

        (await act.Should().ThrowAsync<LanSyncException>()).Which.Failure.Should().Be(LanFailure.Rejected);
    }

    [Fact]
    public async Task AWrongSecret_IsRefused()
    {
        var info = Offer() with { Secret = Convert.ToBase64String(new byte[16]) };

        var act = () => LanSyncSession.PairAsync(info, "phone", default);

        (await act.Should().ThrowAsync<LanSyncException>()).Which.Failure.Should().Be(LanFailure.Rejected);
    }

    [Fact]
    public async Task AnotherCertificateThanTheCodeNames_IsNotTrusted()
    {
        var info = Offer() with { Fingerprint = new string('A', 64) };

        var act = () => LanSyncSession.PairAsync(info, "phone", default);

        (await act.Should().ThrowAsync<LanSyncException>()).Which.Failure.Should().Be(LanFailure.CertificateMismatch);
    }

    [Fact]
    public async Task APairedDevice_ConnectsWithTheKey_AnotherKeyIsRefused()
    {
        var info = Offer();
        var (first, key) = await LanSyncSession.PairAsync(info, "phone", default);
        await first.DisposeAsync();

        await using (var session = await LanSyncSession.ConnectAsync(Loopback, info.Port, info.Fingerprint, "phone", key))
            session.PeerDeviceId.Should().Be(_a.DeviceId);

        var wrong = () => LanSyncSession.ConnectAsync(Loopback, info.Port, info.Fingerprint, "phone", SyncCrypto.NewKey());
        (await wrong.Should().ThrowAsync<LanSyncException>()).Which.Failure.Should().Be(LanFailure.Rejected);
    }

    [Fact]
    public async Task NobodyListening_IsReportedAsUnreachable()
    {
        var info = Offer();
        await _server.DisposeAsync();

        var act = () => LanSyncSession.ConnectAsync(Loopback, info.Port, info.Fingerprint, "phone", _key);

        (await act.Should().ThrowAsync<LanSyncException>()).Which.Failure.Should().Be(LanFailure.Unreachable);
    }

    [Fact]
    public async Task AFileNamedLikeAPath_IsNotWrittenOutsideTheStore()
    {
        var info = Offer();
        var (session, _) = await LanSyncSession.PairAsync(info, "phone", default);
        await using var _ = session;

        var act = () => session.WriteAsync("../evil", 1, [1, 2, 3]);

        await act.Should().ThrowAsync<LanSyncException>();
        Directory.Exists(Path.Combine(Path.GetTempPath(), "evil")).Should().BeFalse();
    }

    [Fact]
    public async Task TwoDevices_KeepInStepThroughTheLink_BothWays()
    {
        _a.AddCategoryAsync("Test Pets").Wait();
        var b = await SyncTestDevice.CreateAsync(_key, TimeProvider.System);
        _disposables.Add(b);

        // Pairing: scan, take the other device's data
        var info = Offer();
        var (pairing, key) = await LanSyncSession.PairAsync(info, b.DeviceId, default);
        await b.Engine.ApplySnapshotAsync(await pairing.SnapshotAsync(), replaceLocalData: true);
        await pairing.DisposeAsync();
        key.Should().Equal(_key);

        // Both change things, then B connects and exchanges files; A applies what arrived
        var storeChanged = new TaskCompletionSource();
        _server.StoreChanged += () => storeChanged.TrySetResult();
        await _a.AddCategoryAsync("Test From PC");
        await b.AddCategoryAsync("Test From Phone");
        await _a.Engine.ExportAsync(_a.Store);
        await b.Engine.ExportAsync(b.Store);

        await using (var session = await LanSyncSession.ConnectAsync(Loopback, info.Port, info.Fingerprint, b.DeviceId, key))
        {
            await SyncStores.CopyMissingAsync(b.Store, session);
            await SyncStores.CopyMissingAsync(session, b.Store);
        }
        await storeChanged.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await b.Engine.ApplyAsync(b.Store);
        await _a.Engine.ApplyAsync(_a.Store);

        (await b.DescribeAsync()).Should().Be(await _a.DescribeAsync());
        (await _a.Db.Table<Category>().ToListAsync()).Select(c => c.Name).Should().Contain(["Test From Phone", "Test From PC", "Test Pets"]);
    }

    [Fact]
    public async Task Frames_RefuseABadLength()
    {
        var bad = new MemoryStream(BitConverter.GetBytes(int.MaxValue));

        var act = () => LanFrames.ReadAsync(bad, default);

        await act.Should().ThrowAsync<InvalidDataException>();
    }
}
