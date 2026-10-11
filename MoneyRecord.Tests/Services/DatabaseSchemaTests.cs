using FluentAssertions;
using MoneyRecord.Models;
using MoneyRecord.Services.Repositories;
using MoneyRecord.Services.Repositories.Migrations;
using SQLite;
using Xunit;

namespace MoneyRecord.Tests.Services;

/// <summary>
/// DatabaseInitializer.CreateSchemaAsync against a real temp SQLite file: new databases are built from the
/// models and stamped; databases from older app versions are migrated.
/// </summary>
public sealed class DatabaseSchemaTests : IAsyncLifetime
{
    private static readonly string[] LookupIndexes =
    [
        "IX_Transaction_Date", "IX_Transaction_AccountId", "IX_Transaction_CategoryId",
        "IX_Transfer_Date", "IX_Transfer_SourceAccountId", "IX_Transfer_DestinationAccountId"
    ];

    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"moneyrecord-test-{Guid.NewGuid():N}.db3");
    private SQLiteAsyncConnection _db = null!;

    public Task InitializeAsync()
    {
        _db = new SQLiteAsyncConnection(_dbPath);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _db.CloseAsync();
        foreach (var file in Directory.GetFiles(Path.GetDirectoryName(_dbPath)!, Path.GetFileName(_dbPath) + "*"))
            File.Delete(file);
    }

    [Fact]
    public async Task NewDatabase_IsStampedWithoutRunningMigrations()
    {
        // A pending migration that would fail proves migrations are skipped on a new database
        var runner = new MigrationRunner([new M001_Baseline(), new FailingMigration(2)]);

        await DatabaseInitializer.CreateSchemaAsync(_db, runner);

        (await MigrationRunner.GetVersionAsync(_db)).Should().Be(2);
    }

    [Fact]
    public async Task NewDatabase_HasLookupIndexes()
    {
        await DatabaseInitializer.CreateSchemaAsync(_db, new MigrationRunner(MigrationRunner.All));

        (await GetIndexNamesAsync()).Should().Contain(LookupIndexes);
    }

    [Fact]
    public async Task UnversionedDatabaseFromOlderApp_IsMigratedAndKeepsRows()
    {
        // Tables as an older app version created them: no indexes, user_version 0
        await _db.ExecuteAsync(
            "CREATE TABLE \"Transaction\" (Id INTEGER PRIMARY KEY AUTOINCREMENT, Date BIGINT, Description VARCHAR, " +
            "Amount FLOAT, CategoryId INTEGER, Type INTEGER, AccountId INTEGER)");
        await _db.ExecuteAsync(
            "CREATE TABLE \"Transfer\" (Id INTEGER PRIMARY KEY AUTOINCREMENT, Date BIGINT, Amount FLOAT, " +
            "SourceAccountId INTEGER, DestinationAccountId INTEGER, Description VARCHAR)");
        await _db.ExecuteAsync(
            "INSERT INTO \"Transaction\" (Date, Description, Amount, CategoryId, Type, AccountId) VALUES (?, 'Lunch', 12.34, 1, 1, 1)",
            DateTime.Today.Ticks);

        await DatabaseInitializer.CreateSchemaAsync(_db, new MigrationRunner(MigrationRunner.All));

        (await MigrationRunner.GetVersionAsync(_db)).Should().Be(MigrationRunner.All.Count);
        (await GetIndexNamesAsync()).Should().Contain(LookupIndexes);
        var rows = await _db.Table<Transaction>().ToListAsync();
        rows.Should().ContainSingle().Which.Description.Should().Be("Lunch");
    }

    [Fact]
    public async Task ExistingDatabase_RunsPendingMigrations()
    {
        await DatabaseInitializer.CreateSchemaAsync(_db, new MigrationRunner([new M001_Baseline()]));

        var act = () => DatabaseInitializer.CreateSchemaAsync(_db, new MigrationRunner([new M001_Baseline(), new FailingMigration(2)]));

        await act.Should().ThrowAsync<InvalidOperationException>("v2 is pending on an existing database, so it must run");
        (await MigrationRunner.GetVersionAsync(_db)).Should().Be(1);
    }

    private Task<List<string>> GetIndexNamesAsync() =>
        _db.QueryScalarsAsync<string>("SELECT name FROM sqlite_master WHERE type = 'index'");

    private sealed class FailingMigration(int version) : IMigration
    {
        public int Version => version;
        public bool RewritesData => false;
        public void Up(SQLiteConnection db) => throw new InvalidOperationException("must not run");
    }
}
