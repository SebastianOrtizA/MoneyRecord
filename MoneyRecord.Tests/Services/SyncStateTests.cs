using AwesomeAssertions;
using MoneyRecord.Models;
using MoneyRecord.Services;
using MoneyRecord.Services.Repositories;
using SQLite;
using Xunit;

namespace MoneyRecord.Tests.Services;

/// <summary>Stage 26.1: the triggers that notice local changes, and the sync side tables, on a real temp SQLite file.</summary>
public sealed class SyncStateTests : IAsyncLifetime
{
    private readonly string _appData = Path.Combine(Path.GetTempPath(), $"moneyrecord-sync-{Guid.NewGuid():N}");
    private readonly SettableTime _time = new() { Now = new DateTimeOffset(2026, 11, 14, 12, 0, 0, TimeSpan.Zero) };
    private DatabaseInitializer _database = null!;
    private SyncStateService _sync = null!;
    private SQLiteAsyncConnection Db { get; set; } = null!;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_appData);
        _database = new DatabaseInitializer(new AppPaths(_appData));
        Db = await _database.GetConnectionAsync();
        _sync = new SyncStateService(_database, _time);
    }

    public async Task DisposeAsync()
    {
        await Db.CloseAsync();
        Directory.Delete(_appData, recursive: true);
    }

    private sealed class SettableTime : TimeProvider
    {
        public DateTimeOffset Now { get; set; }
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private Task<List<SyncMeta>> MetaAsync(string table) => Db.QueryAsync<SyncMeta>("SELECT * FROM SyncMeta WHERE TableName = ?", table);

    private Task<int> TriggerCountAsync() => Db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'trigger'");

    [Fact]
    public async Task SyncIsOffByDefault_NothingIsRecorded()
    {
        await Db.InsertAsync(new Category { Name = "Pets" });

        (await _sync.IsEnabledAsync()).Should().BeFalse();
        (await TriggerCountAsync()).Should().Be(0);
        (await Db.Table<SyncDirty>().CountAsync()).Should().Be(0);
        (await _sync.MaterializeAsync()).Should().Be(0);
    }

    [Fact]
    public async Task EveryTableIsEitherSyncedOrExcluded_WithAnIntegerKey()
    {
        var known = SyncCapture.SyncedTables.Concat(SyncCapture.ExcludedTables).Select(SyncCapture.TableName).ToHashSet();
        var tables = await Db.QueryScalarsAsync<string>("SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%'");

        tables.Should().BeSubsetOf(known, "a new table has to be decided on: synced or not");
        SyncCapture.SyncedTables.Select(t => Db.GetConnection().GetMapping(t).PK?.ColumnType)
            .Should().OnlyContain(t => t == typeof(int));
    }

    [Fact]
    public async Task Enabling_MarksExistingRowsAndGivesEachAGlobalId()
    {
        var categories = await Db.Table<Category>().CountAsync();
        await Db.InsertAsync(new Category { Name = "Pets" });

        await _sync.EnableAsync();
        await _sync.EnableAsync(); // already on: nothing changes

        (await _sync.IsEnabledAsync()).Should().BeTrue();
        (await _sync.GetDeviceIdAsync()).Should().NotBeNullOrEmpty();
        (await _sync.MaterializeAsync()).Should().BeGreaterThanOrEqualTo(categories + 1);
        var device = await _sync.GetDeviceIdAsync();

        var meta = await MetaAsync("Category");
        meta.Should().HaveCount(categories + 1);
        meta.Select(m => m.GlobalId).Should().OnlyHaveUniqueItems();
        meta.Should().OnlyContain(m => m.Dirty && !m.Deleted && m.DeviceId == device);
        (await _sync.MaterializeAsync()).Should().Be(0);
    }

    [Fact]
    public async Task RawSqlWrites_AreCaught_InsertUpdateAndDelete()
    {
        await _sync.EnableAsync();
        await _sync.MaterializeAsync();

        await Db.ExecuteAsync("INSERT INTO Payee (Name) VALUES ('Corner Bakery')");
        (await _sync.MaterializeAsync()).Should().Be(1);
        var inserted = (await MetaAsync("Payee")).Single();
        var firstClock = inserted.Clock;

        _time.Now = _time.Now.AddSeconds(5);
        await Db.ExecuteAsync("UPDATE Payee SET Name = 'Bakery' WHERE Id = ?", inserted.LocalId);
        (await _sync.MaterializeAsync()).Should().Be(1);
        var updated = (await MetaAsync("Payee")).Single();
        updated.GlobalId.Should().Be(inserted.GlobalId, "the same row keeps its global id");
        updated.Clock.Should().BeGreaterThan(firstClock);

        await Db.ExecuteAsync("DELETE FROM Payee WHERE Id = ?", inserted.LocalId);
        (await _sync.MaterializeAsync()).Should().Be(1);
        var deleted = (await MetaAsync("Payee")).Single();
        deleted.Deleted.Should().BeTrue();
        deleted.Dirty.Should().BeTrue();
        deleted.Payload.Should().Contain("\"Name\":\"Bakery\"", "the last values are kept to undo a wrong delete");
    }

    [Fact]
    public async Task AddedAndDeletedBeforeTheNextCollect_LeavesNothingToSend()
    {
        await _sync.EnableAsync();
        await _sync.MaterializeAsync();

        await Db.ExecuteAsync("INSERT INTO Payee (Name) VALUES ('Gone')");
        await Db.ExecuteAsync("DELETE FROM Payee");

        (await _sync.MaterializeAsync()).Should().Be(0);
        (await MetaAsync("Payee")).Should().BeEmpty();
    }

    [Fact]
    public async Task ChangesMadeWhileApplyingAnotherDevicesChanges_AreNotLocalEdits()
    {
        await _sync.EnableAsync();
        await _sync.MaterializeAsync();

        await Db.ExecuteAsync("UPDATE SyncState SET Applying = 1");
        await Db.ExecuteAsync("INSERT INTO Payee (Name) VALUES ('From another device')");
        await Db.ExecuteAsync("UPDATE SyncState SET Applying = 0");

        (await _sync.MaterializeAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Clock_NeverGoesBack_EvenWithinOneMillisecondOrWhenTheWallClockMovesBack()
    {
        await _sync.EnableAsync();
        await _sync.MaterializeAsync();
        await Db.ExecuteAsync("INSERT INTO Payee (Name) VALUES ('a')");
        await _sync.MaterializeAsync();
        var first = (await MetaAsync("Payee")).Single().Clock;

        _time.Now = _time.Now.AddHours(-1); // the phone's clock was set back
        await Db.ExecuteAsync("UPDATE Payee SET Name = 'b'");
        await _sync.MaterializeAsync();

        (await MetaAsync("Payee")).Single().Clock.Should().BeGreaterThan(first);
    }

    [Fact]
    public async Task Disabling_RemovesTheTriggersAndSyncData_KeepsTheRows()
    {
        var accounts = await Db.Table<Account>().CountAsync();
        await _sync.EnableAsync();
        await _sync.MaterializeAsync();

        await _sync.DisableAsync();

        (await _sync.IsEnabledAsync()).Should().BeFalse();
        (await TriggerCountAsync()).Should().Be(0);
        (await Db.Table<SyncMeta>().CountAsync()).Should().Be(0);
        (await Db.Table<Account>().CountAsync()).Should().Be(accounts);
    }

    [Fact]
    public async Task TriggersAreRebuilt_WhenTheirSqlNoLongerMatchesTheTables()
    {
        await _sync.EnableAsync();
        await Db.ExecuteAsync("UPDATE SyncState SET TriggerHash = 'old'");

        await Db.RunInTransactionAsync(SyncCapture.EnsureCurrent);

        var hash = (await Db.FindAsync<SyncState>(1))!.TriggerHash;
        hash.Should().NotBe("old").And.HaveLength(64);
        (await TriggerCountAsync()).Should().Be(SyncCapture.SyncedTables.Count * 3);
    }

    [Fact]
    public async Task RestoringABackup_TurnsSyncOffWithANewDeviceId_AndSendsNothing()
    {
        var paths = new AppPaths(_appData);
        var service = new DataExportService(_database, new AccountRepository(_database), new CategoryRepository(_database),
            new TransactionRepository(_database), new TransferRepository(_database), new PayeeRepository(_database), paths,
            new BackupHistory(new FakePreferences(), TimeProvider.System), new ReceiptStore(paths));
        Directory.CreateDirectory(Path.Combine(_appData, "out"));
        var backup = Path.Combine(_appData, "out", "b.zip");
        await service.ExportBackupAsync(backup);
        await _sync.EnableAsync();
        await _sync.MaterializeAsync();
        var device = await _sync.GetDeviceIdAsync();

        await service.RestoreAsync(backup);

        (await _sync.IsEnabledAsync()).Should().BeFalse();
        (await TriggerCountAsync()).Should().Be(0);
        (await Db.Table<SyncDirty>().CountAsync()).Should().Be(0);
        (await Db.Table<SyncMeta>().CountAsync()).Should().Be(0);

        await _sync.EnableAsync();
        (await _sync.GetDeviceIdAsync()).Should().NotBe(device);
    }
}
