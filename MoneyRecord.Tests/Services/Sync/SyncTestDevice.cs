using MoneyRecord.Models;
using MoneyRecord.Services;
using MoneyRecord.Services.Repositories;
using MoneyRecord.Services.Sync;
using SQLite;

namespace MoneyRecord.Tests.Services.Sync;

/// <summary>One device of a sync test: its own app-data folder with a real database, a local change-file store, and an engine.</summary>
internal sealed class SyncTestDevice : IAsyncDisposable
{
    private readonly string _folder;
    private readonly SyncStateService _state;

    private SyncTestDevice(SyncStateService state, SyncEngine engine, string folder, SQLiteAsyncConnection db)
    {
        _state = state;
        Engine = engine;
        _folder = folder;
        Db = db;
        Store = new DirectorySyncStore(Path.Combine(folder, "sync"));
    }

    public SyncEngine Engine { get; }
    public SQLiteAsyncConnection Db { get; }
    public DirectorySyncStore Store { get; }
    public string DeviceId { get; private set; } = "";

    public static async Task<SyncTestDevice> CreateAsync(byte[] key, TimeProvider time)
    {
        var folder = Path.Combine(Path.GetTempPath(), $"moneyrecord-sync-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        var database = new DatabaseInitializer(new AppPaths(folder));
        var db = await database.GetConnectionAsync();
        var state = new SyncStateService(database, time);
        await state.EnableAsync();
        var device = new SyncTestDevice(state, new SyncEngine(database, state, key, time), folder, db);
        device.DeviceId = await state.GetDeviceIdAsync();
        return device;
    }

    public async ValueTask DisposeAsync()
    {
        await Db.CloseAsync();
        try { Directory.Delete(_folder, recursive: true); } catch (IOException) { }
    }

    /// <summary>Collects local changes (as every sync run does first) and returns how many rows changed.</summary>
    public Task<int> CollectAsync() => _state.MaterializeAsync();

    /// <summary>This device's changes into its store, then the shared store gets them, then the other devices' files come back and apply; returns how many rows were sent and files applied.</summary>
    public async Task<int> SyncThroughAsync(ISyncStore shared, int compactEvery = SyncEngine.CompactEvery)
    {
        var sent = await Engine.ExportAsync(Store);
        await Engine.CompactAsync(Store, compactEvery);
        await SyncStores.CopyMissingAsync(Store, shared);
        await SyncStores.CopyMissingAsync(shared, Store);
        var applied = await Engine.ApplyAsync(Store);
        await Engine.PruneAsync(Store);
        await Engine.PruneAsync(shared);
        return sent + applied;
    }

    public Task<string> DescribeAsync() => Engine.DescribeAsync();

    // ---- Local writes, as the app's services do them (raw rows) ----

    public async Task<int> AddAccountAsync(string name)
    {
        var row = new Account { Name = name, CurrencyCode = "EUR" };
        await Db.InsertAsync(row);
        return row.Id;
    }

    public async Task<int> AddCategoryAsync(string name)
    {
        var row = new Category { Name = name, Type = CategoryType.Expense };
        await Db.InsertAsync(row);
        return row.Id;
    }

    public async Task<int> AddPayeeAsync(string name)
    {
        var row = new Payee { Name = name };
        await Db.InsertAsync(row);
        return row.Id;
    }

    public async Task<int> AddTransactionAsync(int categoryId, int accountId, long cents, int? payeeId = null)
    {
        var row = new Transaction { CategoryId = categoryId, AccountId = accountId, AmountCents = cents, PayeeId = payeeId, Type = TransactionType.Expense, Date = new DateTime(2026, 11, 1) };
        await Db.InsertAsync(row);
        return row.Id;
    }

    public async Task<int> AddTransferAsync(int fromAccountId, int toAccountId, long cents)
    {
        var row = new Transfer { SourceAccountId = fromAccountId, DestinationAccountId = toAccountId, AmountCents = cents, DestinationAmountCents = cents, Date = new DateTime(2026, 11, 2) };
        await Db.InsertAsync(row);
        return row.Id;
    }

    public async Task<int> AddBudgetAsync(int categoryId, long limitCents)
    {
        var row = new Budget { CategoryId = categoryId, LimitAmountCents = limitCents };
        await Db.InsertAsync(row);
        return row.Id;
    }

    public Task<int> UpdateAsync(string table, int id, string column, object? value) =>
        Db.ExecuteAsync($"UPDATE \"{table}\" SET \"{column}\" = ? WHERE Id = ?", value, id);

    public Task<int> DeleteAsync(string table, int id) => Db.ExecuteAsync($"DELETE FROM \"{table}\" WHERE Id = ?", id);

    /// <summary>The local ID of the row with this name here (IDs differ between devices).</summary>
    public Task<int> IdOfAsync(string table, string name) => Db.ExecuteScalarAsync<int>($"SELECT Id FROM \"{table}\" WHERE Name = ?", name);

    public Task<List<int>> IdsAsync(string table) => Db.QueryScalarsAsync<int>($"SELECT Id FROM \"{table}\" ORDER BY Id");

    /// <summary>True when something here still points at the row (the app refuses such deletes).</summary>
    public async Task<bool> IsUsedAsync(string table, int id)
    {
        foreach (var (child, column) in SyncSchema.ReferencesTo(table))
        {
            if (await Db.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM \"{child}\" WHERE \"{column}\" = ?", id) > 0)
                return true;
        }
        return false;
    }

    /// <summary>Rows pointing at a row that is not there (must never happen after a sync).</summary>
    public async Task<int> DanglingReferencesAsync()
    {
        var total = 0;
        foreach (var (child, columns) in SyncSchema.ForeignKeys)
        {
            foreach (var (column, parent) in columns)
                total += await Db.ExecuteScalarAsync<int>(
                    $"SELECT COUNT(*) FROM \"{child}\" WHERE \"{column}\" IS NOT NULL AND \"{column}\" <> 0 AND \"{column}\" NOT IN (SELECT Id FROM \"{parent}\")");
        }
        return total;
    }
}
