using AwesomeAssertions;
using MoneyRecord.Models;
using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.Services.Repositories;
using MoneyRecord.Services.Repositories.Migrations;
using SQLite;
using Xunit;

namespace MoneyRecord.Tests.Services;

/// <summary>The optional database encryption of <see cref="DatabaseInitializer"/>, on temp SQLite files.</summary>
public sealed class DatabaseEncryptionTests : IAsyncLifetime
{
    private readonly string _appData = Path.Combine(Path.GetTempPath(), $"moneyrecord-crypt-{Guid.NewGuid():N}");
    private readonly AppPaths _paths;
    private readonly FakeKeyStore _keyStore = new();
    private readonly List<DatabaseInitializer> _opened = [];

    public DatabaseEncryptionTests()
    {
        _paths = new AppPaths(_appData);
    }

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(_appData);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        foreach (var db in _opened)
            await (await db.GetConnectionAsync()).CloseAsync();
        Directory.Delete(_appData, recursive: true);
    }

    /// <summary>A new initializer over the same files: what the next app start sees.</summary>
    private DatabaseInitializer Start()
    {
        var db = new DatabaseInitializer(_paths, _keyStore);
        _opened.Add(db);
        return db;
    }

    private static async Task<int> AddAccountAsync(DatabaseInitializer db, string name)
    {
        await new AccountRepository(db).SaveAsync(new Account { Name = name, InitialBalance = 12.34m });
        return (await new AccountRepository(db).GetAllAsync()).Count;
    }

    private static bool IsPlainSqliteFile(string path) =>
        File.ReadAllBytes(path).AsSpan(0, 16).SequenceEqual("SQLite format 3\0"u8);

    [Fact]
    public async Task Encrypt_moves_the_data_to_an_encrypted_file_and_removes_the_plain_one()
    {
        var db = Start();
        var accounts = await AddAccountAsync(db, "Savings");

        await db.EncryptAsync();

        db.IsEncrypted.Should().BeTrue();
        File.Exists(_paths.DatabasePath).Should().BeFalse();
        IsPlainSqliteFile(_paths.EncryptedDatabasePath).Should().BeFalse();
        _keyStore.Key.Should().HaveLength(64);
        // The same initializer reopens the encrypted file, and so does the next start
        (await new AccountRepository(db).GetAllAsync()).Should().HaveCount(accounts);
        (await new AccountRepository(Start()).GetAllAsync()).Should().Contain(a => a.Name == "Savings" && a.InitialBalance == 12.34m);
    }

    [Fact]
    public async Task The_encrypted_file_cannot_be_opened_without_its_key()
    {
        var db = Start();
        await db.GetConnectionAsync();
        await db.EncryptAsync();
        await (await db.GetConnectionAsync()).CloseAsync();

        var plain = new SQLiteConnection(_paths.EncryptedDatabasePath);
        var read = () => plain.ExecuteScalar<int>("SELECT COUNT(*) FROM sqlite_master");
        read.Should().Throw<SQLiteException>();
        plain.Close();
    }

    [Fact]
    public async Task Decrypt_brings_the_plain_file_back_and_forgets_the_key()
    {
        var db = Start();
        await AddAccountAsync(db, "Savings");
        await db.EncryptAsync();
        await AddAccountAsync(db, "Wallet"); // written while encrypted

        await db.DecryptAsync();

        db.IsEncrypted.Should().BeFalse();
        File.Exists(_paths.EncryptedDatabasePath).Should().BeFalse();
        IsPlainSqliteFile(_paths.DatabasePath).Should().BeTrue();
        _keyStore.Key.Should().BeNull();
        (await new AccountRepository(Start()).GetAllAsync()).Select(a => a.Name).Should().Contain(["Savings", "Wallet"]);
    }

    [Fact]
    public async Task Encrypt_removes_the_plain_migration_copies()
    {
        var copy = MigrationRunner.GetBackupPath(_paths.DatabasePath, 2);
        var db = Start();
        await db.GetConnectionAsync();
        await File.WriteAllTextAsync(copy, "old plain data");

        await db.EncryptAsync();

        File.Exists(copy).Should().BeFalse();
    }

    [Fact]
    public async Task A_migration_copy_of_the_encrypted_database_stays_encrypted()
    {
        // MigrationRunner copies with VACUUM INTO before a data rewrite: a plain copy would undo the encryption
        var db = Start();
        await AddAccountAsync(db, "Savings");
        await db.EncryptAsync();
        var copy = MigrationRunner.GetBackupPath(_paths.EncryptedDatabasePath, 4);

        await (await db.GetConnectionAsync()).ExecuteAsync("VACUUM INTO ?", copy);

        IsPlainSqliteFile(copy).Should().BeFalse();
        var plain = new SQLiteConnection(copy);
        var read = () => plain.ExecuteScalar<int>("SELECT COUNT(*) FROM sqlite_master");
        read.Should().Throw<SQLiteException>();
        plain.Close();
    }

    [Fact]
    public async Task A_missing_key_sets_the_file_aside_and_starts_a_new_database_once()
    {
        var db = Start();
        await AddAccountAsync(db, "Savings");
        await db.EncryptAsync();
        await (await db.GetConnectionAsync()).CloseAsync();
        _keyStore.Key = null;

        var next = Start();
        var accounts = await new AccountRepository(next).GetAllAsync();

        accounts.Should().NotContain(a => a.Name == "Savings"); // only the seeded Cash account
        next.IsEncrypted.Should().BeFalse();
        Directory.GetFiles(_appData, AppPaths.EncryptedDatabaseFileName + DatabaseInitializer.UnreadableSuffix + "-*")
            .Should().ContainSingle();
        next.TakeUnreadableDatabaseNotice().Should().BeTrue();
        next.TakeUnreadableDatabaseNotice().Should().BeFalse();
    }

    [Fact]
    public async Task A_wrong_key_sets_the_file_aside_like_a_missing_one()
    {
        var db = Start();
        await db.GetConnectionAsync();
        await db.EncryptAsync();
        await (await db.GetConnectionAsync()).CloseAsync();
        _keyStore.Key = new string('A', 64); // "file is not a database"

        var next = Start();
        await next.GetConnectionAsync();

        next.IsEncrypted.Should().BeFalse();
        next.TakeUnreadableDatabaseNotice().Should().BeTrue();
    }

    [Fact]
    public async Task A_plain_file_left_next_to_a_readable_encrypted_one_is_deleted()
    {
        var db = Start();
        await AddAccountAsync(db, "Savings");
        await db.EncryptAsync();
        await (await db.GetConnectionAsync()).CloseAsync();
        await File.WriteAllTextAsync(_paths.DatabasePath, "leftover"); // a switch that stopped before its last step

        var next = Start();
        (await new AccountRepository(next).GetAllAsync()).Should().Contain(a => a.Name == "Savings");

        File.Exists(_paths.DatabasePath).Should().BeFalse();
        next.TakeUnreadableDatabaseNotice().Should().BeFalse();
    }

    [Fact]
    public async Task A_failed_key_store_leaves_the_plain_database_as_it_was()
    {
        var db = Start();
        await AddAccountAsync(db, "Savings");
        _keyStore.FailOnSet = true;

        var encrypt = () => db.EncryptAsync();

        await encrypt.Should().ThrowAsync<IOException>();
        db.IsEncrypted.Should().BeFalse();
        Directory.GetFiles(_appData, "*.tmp").Should().BeEmpty();
        (await new AccountRepository(db).GetAllAsync()).Should().Contain(a => a.Name == "Savings");
    }

    [Fact]
    public void Without_a_key_store_encryption_is_not_offered()
    {
        new DatabaseInitializer(_paths).IsSupported.Should().BeFalse();
        Start().IsSupported.Should().BeTrue();
    }

    [Fact]
    public async Task Setup_is_not_offered_again_when_only_the_encrypted_database_exists()
    {
        var db = Start();
        await db.GetConnectionAsync();
        await db.EncryptAsync();
        var setup = new SetupService(new FakePreferences(), _paths, db,
            new AccountRepository(db), new CategoryRepository(db));

        setup.ResolveFirstRunPending().Should().BeFalse();
    }

    private sealed class FakeKeyStore : IDatabaseKeyStore
    {
        public string? Key { get; set; }

        public bool FailOnSet { get; set; }

        public bool IsSupported => true;

        public Task<string?> GetAsync() => Task.FromResult(Key);

        public Task SetAsync(string key)
        {
            if (FailOnSet)
                throw new IOException("Keystore unavailable");
            Key = key;
            return Task.CompletedTask;
        }

        public void Remove() => Key = null;
    }
}
