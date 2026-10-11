using AwesomeAssertions;
using MoneyRecord.Models;
using MoneyRecord.Services.Repositories.Migrations;
using SQLite;
using Xunit;

namespace MoneyRecord.Tests.Services;

public sealed class MigrationRunnerTests : IAsyncLifetime
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"moneyrecord-test-{Guid.NewGuid():N}.db3");
    private SQLiteAsyncConnection _db = null!;

    public async Task InitializeAsync()
    {
        _db = new SQLiteAsyncConnection(_dbPath);
        // The pre-migration (v1) schema, as created by DatabaseInitializer
        await _db.CreateTableAsync<Category>();
        await _db.CreateTableAsync<Transaction>();
        await _db.CreateTableAsync<Account>();
        await _db.CreateTableAsync<Transfer>();
        await _db.CreateTableAsync<Budget>();
        await _db.CreateTableAsync<RecurringTransaction>();
    }

    public async Task DisposeAsync()
    {
        await _db.CloseAsync();
        foreach (var file in Directory.GetFiles(Path.GetDirectoryName(_dbPath)!, Path.GetFileName(_dbPath) + "*"))
            File.Delete(file);
    }

    [Fact]
    public async Task RunAsync_ExistingUnversionedDatabase_MigratesToLatest()
    {
        var runner = new MigrationRunner(MigrationRunner.All);

        var version = await runner.RunAsync(_db);

        version.Should().Be(runner.LatestVersion);
        (await MigrationRunner.GetVersionAsync(_db)).Should().Be(runner.LatestVersion);
    }

    [Fact]
    public async Task RunAsync_KeepsExistingRows()
    {
        await _db.InsertAsync(new Transaction { Description = "Lunch", Amount = 12.34m, Date = DateTime.Today });

        await new MigrationRunner(MigrationRunner.All).RunAsync(_db);

        var rows = await _db.Table<Transaction>().ToListAsync();
        rows.Should().ContainSingle().Which.Amount.Should().Be(12.34m);
    }

    [Fact]
    public async Task RunAsync_Twice_IsANoOp()
    {
        var runner = new MigrationRunner(MigrationRunner.All);
        await runner.RunAsync(_db);

        var second = await runner.RunAsync(_db);

        second.Should().Be(runner.LatestVersion);
    }

    [Fact]
    public async Task RunAsync_FailingMigration_RollsBackAndKeepsVersion()
    {
        var runner = new MigrationRunner([new M001_Baseline(), new FakeMigration(2, fail: true)]);

        var act = () => runner.RunAsync(_db);

        await act.Should().ThrowAsync<InvalidOperationException>();
        (await MigrationRunner.GetVersionAsync(_db)).Should().Be(1);
        var tables = await _db.QueryScalarsAsync<string>("SELECT name FROM sqlite_master WHERE name = 'Fake'");
        tables.Should().BeEmpty("the failed step's changes must be rolled back");
    }

    [Fact]
    public async Task RunAsync_DataRewritingMigration_BacksUpFirst()
    {
        await _db.InsertAsync(new Account { Name = "Bank", InitialBalance = 100m });
        var runner = new MigrationRunner([new M001_Baseline(), new FakeMigration(2, rewritesData: true)]);

        await runner.RunAsync(_db);

        var backupPath = MigrationRunner.GetBackupPath(_dbPath, 2);
        File.Exists(backupPath).Should().BeTrue();

        var backup = new SQLiteAsyncConnection(backupPath);
        try
        {
            (await backup.Table<Account>().CountAsync()).Should().Be(1);
            (await MigrationRunner.GetVersionAsync(backup)).Should().Be(1, "the backup is taken before the step runs");
        }
        finally
        {
            await backup.CloseAsync();
        }
    }

    [Fact]
    public async Task RunAsync_NonRewritingMigration_DoesNotBackUp()
    {
        await new MigrationRunner([new M001_Baseline()]).RunAsync(_db);

        Directory.GetFiles(Path.GetDirectoryName(_dbPath)!, Path.GetFileName(_dbPath) + ".bak-v*").Should().BeEmpty();
    }

    [Fact]
    public async Task RunAsync_DatabaseFromNewerApp_Throws()
    {
        await _db.ExecuteAsync("PRAGMA user_version = 99");

        var act = () => new MigrationRunner(MigrationRunner.All).RunAsync(_db);

        await act.Should().ThrowAsync<InvalidOperationException>();
        (await MigrationRunner.GetVersionAsync(_db)).Should().Be(99);
    }

    [Fact]
    public async Task StampLatestAsync_SetsLatestVersionWithoutRunningMigrations()
    {
        var runner = new MigrationRunner([new M001_Baseline(), new FakeMigration(2, fail: true)]);

        await runner.StampLatestAsync(_db);

        (await MigrationRunner.GetVersionAsync(_db)).Should().Be(2);
    }

    [Fact]
    public void Constructor_RejectsGapsAndDuplicates()
    {
        var gap = () => new MigrationRunner([new M001_Baseline(), new FakeMigration(3)]);
        var duplicate = () => new MigrationRunner([new M001_Baseline(), new FakeMigration(1)]);

        gap.Should().Throw<ArgumentException>();
        duplicate.Should().Throw<ArgumentException>();
    }

    private sealed class FakeMigration(int version, bool fail = false, bool rewritesData = false) : IMigration
    {
        public int Version => version;
        public bool RewritesData => rewritesData;

        public void Up(SQLiteConnection db)
        {
            db.Execute("CREATE TABLE Fake (Id INTEGER)");
            if (fail)
                throw new InvalidOperationException("boom");
        }
    }
}
