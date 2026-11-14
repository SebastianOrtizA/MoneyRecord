using AwesomeAssertions;
using System.IO.Compression;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.Services.Repositories;
using Xunit;

namespace MoneyRecord.Tests.Services;

/// <summary>Receipt files encrypted along with the database (<see cref="ReceiptStore"/>), on temp files.</summary>
public sealed class ReceiptStoreTests : IDisposable
{
    private static readonly byte[] Photo = Enumerable.Range(0, 5000).Select(i => (byte)(i * 7)).ToArray();

    private readonly string _appData = Path.Combine(Path.GetTempPath(), $"moneyrecord-receipts-{Guid.NewGuid():N}");
    private readonly AppPaths _paths;
    private readonly KeyStore _keyStore = new();
    private readonly ReceiptStore _sut;

    public ReceiptStoreTests()
    {
        Directory.CreateDirectory(_appData);
        _paths = new AppPaths(_appData, Path.Combine(_appData, "cache"));
        _sut = new ReceiptStore(_paths, _keyStore);
    }

    public void Dispose() => Directory.Delete(_appData, recursive: true);

    private string PathOf(string name) => Path.Combine(_paths.ReceiptsFolder, name);

    private Task SaveAsync(string name, byte[]? bytes = null) => _sut.SaveAsync(PathOf(name), new MemoryStream(bytes ?? Photo));

    private void TurnOnDatabaseEncryption(string key = "k1")
    {
        _keyStore.Key = key;
        File.WriteAllText(_paths.EncryptedDatabasePath, "x");
    }

    private static async Task<byte[]> ReadAsync(IReceiptStore store, string path)
    {
        await using var stream = await store.OpenReadAsync(path);
        using var copy = new MemoryStream();
        await stream.CopyToAsync(copy);
        return copy.ToArray();
    }

    [Fact]
    public async Task Without_encryption_the_file_is_stored_as_it_is()
    {
        await SaveAsync("a.jpg");

        File.ReadAllBytes(PathOf("a.jpg")).Should().Equal(Photo);
        File.Exists(PathOf("a.jpg.enc")).Should().BeFalse();
        (await ReadAsync(_sut, PathOf("a.jpg"))).Should().Equal(Photo);
    }

    [Fact]
    public async Task With_encryption_the_file_is_stored_encrypted_and_reads_back()
    {
        TurnOnDatabaseEncryption();

        await SaveAsync("a.jpg");

        File.Exists(PathOf("a.jpg")).Should().BeFalse();
        var stored = File.ReadAllBytes(PathOf("a.jpg.enc"));
        stored.AsSpan().IndexOf(Photo.AsSpan(0, 64)).Should().Be(-1, "the photo's bytes are not in the file");
        (await ReadAsync(_sut, PathOf("a.jpg"))).Should().Equal(Photo);
    }

    [Fact]
    public async Task The_same_photo_is_stored_differently_each_time()
    {
        TurnOnDatabaseEncryption();

        await SaveAsync("a.jpg");
        await SaveAsync("b.jpg");

        File.ReadAllBytes(PathOf("a.jpg.enc")).Should().NotEqual(File.ReadAllBytes(PathOf("b.jpg.enc")));
    }

    [Fact]
    public async Task A_changed_encrypted_file_is_refused()
    {
        TurnOnDatabaseEncryption();
        await SaveAsync("a.jpg");
        var bytes = File.ReadAllBytes(PathOf("a.jpg.enc"));
        bytes[^1] ^= 1;
        File.WriteAllBytes(PathOf("a.jpg.enc"), bytes);

        var read = () => ReadAsync(_sut, PathOf("a.jpg"));

        await read.Should().ThrowAsync<System.Security.Cryptography.CryptographicException>();
    }

    [Fact]
    public async Task A_missing_receipt_is_a_file_not_found()
    {
        var read = () => ReadAsync(_sut, PathOf("none.jpg"));

        await read.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task EncryptAll_turns_every_plain_file_into_an_encrypted_one_and_DecryptAll_undoes_it()
    {
        await SaveAsync("a.jpg");
        await SaveAsync("b.pdf", [1, 2, 3]);
        TurnOnDatabaseEncryption();

        await _sut.EncryptAllAsync();

        Directory.GetFiles(_paths.ReceiptsFolder).Select(Path.GetFileName).Should().BeEquivalentTo("a.jpg.enc", "b.pdf.enc");
        (await ReadAsync(_sut, PathOf("a.jpg"))).Should().Equal(Photo);

        await _sut.DecryptAllAsync();

        Directory.GetFiles(_paths.ReceiptsFolder).Select(Path.GetFileName).Should().BeEquivalentTo("a.jpg", "b.pdf");
        File.ReadAllBytes(PathOf("a.jpg")).Should().Equal(Photo);
        File.ReadAllBytes(PathOf("b.pdf")).Should().Equal([1, 2, 3]);
    }

    [Fact]
    public async Task EncryptAll_is_a_no_op_with_no_receipts_folder()
    {
        TurnOnDatabaseEncryption();

        await _sut.EncryptAllAsync();
        await _sut.DecryptAllAsync();

        Directory.Exists(_paths.ReceiptsFolder).Should().BeFalse();
    }

    [Fact]
    public async Task A_new_key_after_turning_encryption_off_and_on_still_reads_new_files()
    {
        TurnOnDatabaseEncryption("first");
        await SaveAsync("a.jpg");
        await _sut.DecryptAllAsync();
        File.Delete(_paths.EncryptedDatabasePath);
        _keyStore.Key = null;

        TurnOnDatabaseEncryption("second");
        await _sut.EncryptAllAsync();

        (await ReadAsync(_sut, PathOf("a.jpg"))).Should().Equal(Photo);
    }

    [Fact]
    public async Task A_plain_copy_wins_over_an_encrypted_twin_and_saving_removes_the_other_kind()
    {
        TurnOnDatabaseEncryption();
        await SaveAsync("a.jpg");
        File.WriteAllBytes(PathOf("a.jpg"), [9, 9]);

        (await ReadAsync(_sut, PathOf("a.jpg"))).Should().Equal([9, 9]);

        await SaveAsync("a.jpg", [5]);

        File.Exists(PathOf("a.jpg")).Should().BeFalse();
        (await ReadAsync(_sut, PathOf("a.jpg"))).Should().Equal([5]);
    }

    [Fact]
    public async Task GetReadablePath_returns_a_stored_plain_file_itself_and_a_decrypted_copy_for_an_encrypted_one()
    {
        await SaveAsync("plain.pdf");
        TurnOnDatabaseEncryption();
        await SaveAsync("secret.pdf");

        (await _sut.GetReadablePathAsync(PathOf("plain.pdf"))).Should().Be(PathOf("plain.pdf"));

        var copy = await _sut.GetReadablePathAsync(PathOf("secret.pdf"));
        copy.Should().StartWith(_paths.ReceiptViewFolder);
        File.ReadAllBytes(copy).Should().Equal(Photo);

        _sut.ClearViewFolder();

        Directory.Exists(_paths.ReceiptViewFolder).Should().BeFalse();
        File.Exists(PathOf("secret.pdf.enc")).Should().BeTrue();
    }

    [Fact]
    public async Task Delete_removes_both_the_plain_and_the_encrypted_file()
    {
        TurnOnDatabaseEncryption();
        await SaveAsync("a.jpg");
        File.WriteAllBytes(PathOf("a.jpg"), [1]);

        ReceiptFiles.Delete(PathOf("a.jpg"), _paths.ReceiptsFolder).Should().BeTrue();

        Directory.GetFiles(_paths.ReceiptsFolder).Should().BeEmpty();
    }

    [Fact]
    public void LogicalName_drops_only_the_encrypted_suffix()
    {
        ReceiptFiles.LogicalName("receipt_1.jpg.enc").Should().Be("receipt_1.jpg");
        ReceiptFiles.LogicalName("receipt_1.jpg").Should().Be("receipt_1.jpg");
    }

    [Fact]
    public async Task Turning_database_encryption_on_and_off_takes_the_receipts_along()
    {
        var db = new DatabaseInitializer(_paths, _keyStore, _sut);
        await SaveAsync("a.jpg");

        await db.EncryptAsync();

        File.Exists(PathOf("a.jpg.enc")).Should().BeTrue();
        File.Exists(PathOf("a.jpg")).Should().BeFalse();
        (await ReadAsync(_sut, PathOf("a.jpg"))).Should().Equal(Photo);

        await SaveAsync("b.jpg");
        File.Exists(PathOf("b.jpg.enc")).Should().BeTrue("a photo saved while encrypted is encrypted");

        await db.DecryptAsync();

        File.ReadAllBytes(PathOf("a.jpg")).Should().Equal(Photo);
        File.ReadAllBytes(PathOf("b.jpg")).Should().Equal(Photo);
        Directory.GetFiles(_paths.ReceiptsFolder, "*.enc").Should().BeEmpty();

        await (await db.GetConnectionAsync()).CloseAsync();
    }

    [Fact]
    public async Task A_damaged_encrypted_file_is_removed_when_decrypting_and_the_rest_still_come_back()
    {
        TurnOnDatabaseEncryption();
        await SaveAsync("good.jpg");
        await SaveAsync("bad.jpg");
        var bytes = File.ReadAllBytes(PathOf("bad.jpg.enc"));
        bytes[^1] ^= 1;
        File.WriteAllBytes(PathOf("bad.jpg.enc"), bytes);

        await _sut.DecryptAllAsync();

        Directory.GetFiles(_paths.ReceiptsFolder).Select(Path.GetFileName).Should().BeEquivalentTo("good.jpg");
    }

    [Fact]
    public async Task A_failed_receipt_decryption_leaves_the_database_encrypted_so_turning_off_can_be_repeated()
    {
        var receipts = new ThrowingOnceStore(_sut);
        var db = new DatabaseInitializer(_paths, _keyStore, receipts);
        await db.EncryptAsync();
        await SaveAsync("a.jpg");

        var turnOff = () => db.DecryptAsync();
        await turnOff.Should().ThrowAsync<IOException>();

        File.Exists(_paths.EncryptedDatabasePath).Should().BeTrue();
        _keyStore.Key.Should().NotBeNull();
        (await ReadAsync(_sut, PathOf("a.jpg"))).Should().Equal(Photo);

        await db.DecryptAsync();

        File.ReadAllBytes(PathOf("a.jpg")).Should().Equal(Photo);
        File.Exists(_paths.EncryptedDatabasePath).Should().BeFalse();
        await (await db.GetConnectionAsync()).CloseAsync();
    }

    [Fact]
    public async Task Starting_with_an_encrypted_database_finishes_an_interrupted_switch()
    {
        var first = new DatabaseInitializer(_paths, _keyStore, _sut);
        await first.EncryptAsync();
        await SaveAsync("late.jpg"); // encrypted
        File.WriteAllBytes(PathOf("left-plain.jpg"), Photo); // what an interrupted EncryptAll leaves

        var second = new DatabaseInitializer(_paths, _keyStore, _sut);
        await second.GetConnectionAsync();

        File.Exists(PathOf("left-plain.jpg")).Should().BeFalse();
        File.Exists(PathOf("left-plain.jpg.enc")).Should().BeTrue();

        await (await first.GetConnectionAsync()).CloseAsync();
        await (await second.GetConnectionAsync()).CloseAsync();
    }

    [Fact]
    public async Task A_backup_of_encrypted_receipts_holds_plain_photos_and_a_restore_encrypts_them_again()
    {
        var db = new DatabaseInitializer(_paths, _keyStore, _sut);
        await db.EncryptAsync();
        var connection = await db.GetConnectionAsync();
        await SaveAsync("r.jpg");
        await connection.InsertAsync(new Transaction { Description = "Lunch", Amount = 5m, ReceiptPhotoPath = PathOf("r.jpg") });
        var service = new DataExportService(db, new AccountRepository(db), new CategoryRepository(db), new TransactionRepository(db),
            new TransferRepository(db), new PayeeRepository(db), _paths, new BackupHistory(new FakePreferences(), TimeProvider.System), _sut);
        var zipPath = Path.Combine(_appData, "backup.zip");

        await service.ExportBackupAsync(zipPath);

        using (var zip = ZipFile.OpenRead(zipPath))
        {
            using var entry = zip.GetEntry("receipts/r.jpg")!.Open();
            using var copy = new MemoryStream();
            entry.CopyTo(copy);
            copy.ToArray().Should().Equal(Photo);
        }

        ReceiptFiles.Delete(PathOf("r.jpg"), _paths.ReceiptsFolder);
        await service.RestoreAsync(zipPath);

        File.Exists(PathOf("r.jpg")).Should().BeFalse();
        File.Exists(PathOf("r.jpg.enc")).Should().BeTrue();
        (await ReadAsync(_sut, PathOf("r.jpg"))).Should().Equal(Photo);
        await connection.CloseAsync();
    }

    /// <summary>A store whose first DecryptAll fails like a disk error would.</summary>
    private sealed class ThrowingOnceStore(IReceiptStore inner) : IReceiptStore
    {
        private bool _thrown;

        public bool IsEncrypting => inner.IsEncrypting;
        public Task SaveAsync(string path, Stream source) => inner.SaveAsync(path, source);
        public Task<Stream> OpenReadAsync(string path) => inner.OpenReadAsync(path);
        public Task<string> GetReadablePathAsync(string path) => inner.GetReadablePathAsync(path);
        public void ClearViewFolder() => inner.ClearViewFolder();
        public Task EncryptAllAsync() => inner.EncryptAllAsync();

        public Task DecryptAllAsync()
        {
            if (_thrown)
                return inner.DecryptAllAsync();
            _thrown = true;
            throw new IOException("disk error");
        }
    }

    private sealed class KeyStore : IDatabaseKeyStore
    {
        public string? Key { get; set; }

        public bool IsSupported => true;

        public Task<string?> GetAsync() => Task.FromResult(Key);

        public Task SetAsync(string key)
        {
            Key = key;
            return Task.CompletedTask;
        }

        public void Remove() => Key = null;
    }
}
