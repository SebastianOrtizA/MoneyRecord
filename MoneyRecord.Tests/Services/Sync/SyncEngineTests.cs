using System.Security.Cryptography;
using System.Text.Json;
using AwesomeAssertions;
using MoneyRecord.Models;
using MoneyRecord.Services.Repositories;
using MoneyRecord.Services.Sync;
using Xunit;

namespace MoneyRecord.Tests.Services.Sync;

/// <summary>Stage 26.2: two or three real databases kept in step through change files, on the app's real schema.</summary>
public sealed class SyncEngineTests : IAsyncLifetime
{
    private readonly byte[] _key = SyncCrypto.NewKey();
    private readonly string _shared = Path.Combine(Path.GetTempPath(), $"moneyrecord-shared-{Guid.NewGuid():N}");
    private readonly List<SyncTestDevice> _devices = [];
    private readonly Clock _time = new() { Now = new DateTimeOffset(2026, 11, 14, 12, 0, 0, TimeSpan.Zero) };

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; }
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private DirectorySyncStore Shared => new(_shared);

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        foreach (var device in _devices)
            await device.DisposeAsync();
        try { Directory.Delete(_shared, recursive: true); } catch (IOException) { }
    }

    private async Task<SyncTestDevice> NewDeviceAsync()
    {
        var device = await SyncTestDevice.CreateAsync(_key, _time);
        _devices.Add(device);
        return device;
    }

    /// <summary>A second device the way pairing makes it: a snapshot of the first replaces its own (seed) data.</summary>
    private async Task<SyncTestDevice> PairedWithAsync(SyncTestDevice first)
    {
        var second = await NewDeviceAsync();
        await second.Engine.ApplySnapshotAsync(await first.Engine.CreateSnapshotAsync(), replaceLocalData: true);
        return second;
    }

    /// <summary>Rounds of syncing until nothing is sent or applied any more (a row brought back by one device is a change the others then apply).</summary>
    private async Task SyncAllAsync(params SyncTestDevice[] devices)
    {
        for (var round = 0; round < 10; round++)
        {
            var activity = 0;
            foreach (var d in devices) activity += await d.SyncThroughAsync(Shared);
            if (activity == 0)
                return;
        }
        throw new InvalidOperationException("The devices keep sending each other changes.");
    }

    [Fact]
    public async Task EveryForeignKeyColumnIsKnown_AndTheTablesCanBeOrdered()
    {
        var db = (await NewDeviceAsync()).Db.GetConnection();
        foreach (var type in SyncCapture.SyncedTables)
        {
            var table = SyncCapture.TableName(type);
            var mapping = db.GetMapping(type);
            SyncSchema.ForeignKeys.Should().ContainKey(table);
            var idColumns = mapping.Columns.Where(c => c != mapping.PK && c.Name.EndsWith("Id", StringComparison.Ordinal)
                && (c.ColumnType == typeof(int) || c.ColumnType == typeof(int?))).Select(c => c.Name);
            idColumns.Should().BeEquivalentTo(SyncSchema.ForeignKeys[table].Keys, $"{table}: an ID column nobody knows the target of would break sync");
            SyncSchema.ForeignKeys[table].Values.Should().OnlyContain(parent => SyncSchema.ForeignKeys.ContainsKey(parent));
        }
        SyncSchema.Order.Should().HaveCount(SyncCapture.SyncedTables.Count);
    }

    [Fact]
    public async Task AnotherDevice_GetsEverythingFromASnapshot_AndItsOwnSeedDataIsReplaced()
    {
        var a = await NewDeviceAsync();
        var account = await a.AddAccountAsync("Savings");
        var category = await a.AddCategoryAsync("Pets");
        await a.AddTransactionAsync(category, account, 1250);
        var b = await PairedWithAsync(a);

        (await b.DescribeAsync()).Should().Be(await a.DescribeAsync());
        (await b.Db.Table<Account>().ToListAsync()).Select(x => x.Name).Should().BeEquivalentTo(await a.Db.Table<Account>().ToListAsync().ContinueWith(t => t.Result.Select(x => x.Name)));
        (await b.DanglingReferencesAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ChangesFlowBothWays_WithDifferentLocalIds()
    {
        var a = await NewDeviceAsync();
        var b = await PairedWithAsync(a);
        await b.AddPayeeAsync("Only on B"); // B's IDs are now ahead of A's

        var account = await a.AddAccountAsync("Cash 2");
        var category = await a.AddCategoryAsync("Gifts");
        var transaction = await a.AddTransactionAsync(category, account, 999);
        await SyncAllAsync(a, b);

        (await b.DescribeAsync()).Should().Be(await a.DescribeAsync());
        (await b.Db.Table<Transaction>().ToListAsync()).Should().Contain(t => t.AmountCents == 999);
        (await a.Db.Table<Payee>().ToListAsync()).Should().Contain(p => p.Name == "Only on B");
        (await b.DanglingReferencesAsync()).Should().Be(0);
        transaction.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ApplyingOtherDevicesChanges_IsNotALocalEdit()
    {
        var a = await NewDeviceAsync();
        var b = await PairedWithAsync(a);
        await a.AddCategoryAsync("From A");
        await a.SyncThroughAsync(Shared);

        await b.SyncThroughAsync(Shared);

        (await b.CollectAsync()).Should().Be(0, "what came from another device is not sent back as a new change");
        (await b.Engine.ExportAsync(b.Store)).Should().Be(0);
    }

    [Fact]
    public async Task ApplyingTheSameFilesAgain_ChangesNothing()
    {
        var a = await NewDeviceAsync();
        var b = await PairedWithAsync(a);
        await a.AddCategoryAsync("One");
        await a.SyncThroughAsync(Shared);
        await b.SyncThroughAsync(Shared);
        var before = await b.DescribeAsync();

        (await b.Engine.ApplyAsync(b.Store)).Should().Be(0);
        await b.SyncThroughAsync(Shared);

        (await b.DescribeAsync()).Should().Be(before);
    }

    [Fact]
    public async Task ConcurrentEdits_LastWriterWins_OnEveryDevice()
    {
        var a = await NewDeviceAsync();
        var category = await a.AddCategoryAsync("Test Food");
        var b = await PairedWithAsync(a);
        var categoryOnB = await b.IdOfAsync("Category", "Test Food");

        await a.UpdateAsync("Category", category, "Name", "Groceries");
        await Task.Delay(30); // a write is stamped when it happens, so the order of the two needs real time between them
        await b.UpdateAsync("Category", categoryOnB, "Name", "Restaurants"); // later
        await SyncAllAsync(a, b);

        (await a.DescribeAsync()).Should().Be(await b.DescribeAsync()).And.Contain("Restaurants");
    }

    [Fact]
    public async Task EditAfterADelete_BringsTheRowBack_DeleteAfterAnEdit_Wins()
    {
        var a = await NewDeviceAsync();
        var account = await a.AddAccountAsync("Cash 2");
        var category = await a.AddCategoryAsync("Test Food");
        var keep = await a.AddTransactionAsync(category, account, 100);
        var drop = await a.AddTransactionAsync(category, account, 200);
        var b = await PairedWithAsync(a);

        await a.DeleteAsync("Transaction", keep);
        await Task.Delay(30); // a write is stamped when it happens, so the order of the two needs real time between them
        var keepOnB = await b.Db.ExecuteScalarAsync<int>("SELECT Id FROM \"Transaction\" WHERE AmountCents = 100");
        var dropOnB = await b.Db.ExecuteScalarAsync<int>("SELECT Id FROM \"Transaction\" WHERE AmountCents = 200");
        await b.UpdateAsync("Transaction", keepOnB, "AmountCents", 111L); // newer than the delete
        await b.UpdateAsync("Transaction", dropOnB, "AmountCents", 222L);
        await a.CollectAsync();
        await b.CollectAsync();
        await Task.Delay(30); // a write is stamped when it happens, so the order of the two needs real time between them
        await a.DeleteAsync("Transaction", drop); // newer than the edit
        await SyncAllAsync(a, b);

        (await a.DescribeAsync()).Should().Be(await b.DescribeAsync());
        (await a.Db.Table<Transaction>().ToListAsync()).Select(t => t.AmountCents).Should().Equal(111L);
    }

    [Fact]
    public async Task CategoryDeletedWhileAnotherDeviceUsesIt_IsKeptEverywhere()
    {
        var a = await NewDeviceAsync();
        var account = await a.AddAccountAsync("Cash 2");
        var category = await a.AddCategoryAsync("Test Food");
        var b = await PairedWithAsync(a);

        await a.DeleteAsync("Category", category);
        await a.CollectAsync();
        await Task.Delay(30); // a write is stamped when it happens, so the order of the two needs real time between them
        var transaction = await b.AddTransactionAsync(await b.IdOfAsync("Category", "Test Food"), await b.IdOfAsync("Account", "Cash 2"), 500); // B has not seen the delete
        await SyncAllAsync(a, b);

        (await a.DescribeAsync()).Should().Be(await b.DescribeAsync());
        (await a.DanglingReferencesAsync()).Should().Be(0);
        (await b.DanglingReferencesAsync()).Should().Be(0);
        (await a.Db.Table<Category>().ToListAsync()).Should().Contain(c => c.Name == "Test Food");

        // B removing that transaction afterwards must not leave the devices disagreeing about the category
        await b.DeleteAsync("Transaction", transaction);
        await SyncAllAsync(a, b);
        (await a.DescribeAsync()).Should().Be(await b.DescribeAsync());
    }

    [Fact]
    public async Task LocalEditNotCollectedYet_BeatsAnOlderRemoteEdit_WhenFilesAreApplied()
    {
        var a = await NewDeviceAsync();
        await a.AddCategoryAsync("Test Cat");
        var b = await PairedWithAsync(a);
        await b.UpdateAsync("Category", await b.IdOfAsync("Category", "Test Cat"), "Name", "Remote");
        await b.Engine.ExportAsync(b.Store);
        await Task.Delay(30);
        await a.UpdateAsync("Category", await a.IdOfAsync("Category", "Test Cat"), "Name", "Local"); // newer, not collected
        await SyncStores.CopyMissingAsync(b.Store, a.Store);

        await a.Engine.ApplyAsync(a.Store); // on its own, as a received file would trigger it

        (await a.Db.Table<Category>().ToListAsync()).Select(c => c.Name).Should().Contain("Local").And.NotContain("Remote");
        await SyncAllAsync(a, b);
        (await b.DescribeAsync()).Should().Be(await a.DescribeAsync()).And.Contain("Name=Local");
    }

    [Fact]
    public async Task LocalDeleteNotCollectedYet_IsNotUndoneByAnOlderRemoteEdit()
    {
        var a = await NewDeviceAsync();
        await a.AddCategoryAsync("Test Cat");
        var b = await PairedWithAsync(a);
        await b.UpdateAsync("Category", await b.IdOfAsync("Category", "Test Cat"), "Name", "Remote");
        await b.Engine.ExportAsync(b.Store);
        await Task.Delay(30);
        await a.DeleteAsync("Category", await a.IdOfAsync("Category", "Test Cat")); // newer, not collected
        await SyncStores.CopyMissingAsync(b.Store, a.Store);

        await a.Engine.ApplyAsync(a.Store);

        (await a.Db.Table<Category>().ToListAsync()).Select(c => c.Name).Should().NotContain(["Test Cat", "Remote"]);
        await SyncAllAsync(a, b);
        (await b.DescribeAsync()).Should().Be(await a.DescribeAsync());
    }

    [Fact]
    public async Task DeviceFromASnapshotTakenAfterADelete_CanStillBringTheRowBack()
    {
        var a = await NewDeviceAsync();
        var account = await a.AddAccountAsync("Cash 2");
        await a.AddCategoryAsync("Test Food");
        var b = await PairedWithAsync(a); // knows the category
        await a.DeleteAsync("Category", await a.IdOfAsync("Category", "Test Food"));
        await a.Engine.ExportAsync(a.Store);
        var fresh = await PairedWithAsync(a); // its snapshot already has the category as deleted
        await b.AddTransactionAsync(await b.IdOfAsync("Category", "Test Food"), await b.IdOfAsync("Account", "Cash 2"), 500);
        await b.Engine.ExportAsync(b.Store);

        await fresh.Store.WriteAsync(b.DeviceId, 1, (await b.Store.ReadAsync(b.DeviceId, 1))!);
        var applied = await fresh.Engine.ApplyAsync(fresh.Store);

        applied.Should().Be(1, "the file is not blocked for good by a deleted row it needs");
        (await fresh.Db.Table<Category>().ToListAsync()).Should().Contain(c => c.Name == "Test Food");
        (await fresh.DanglingReferencesAsync()).Should().Be(0);
        account.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ReplacingMyData_ForgetsWhatWasAppliedFromOtherDevicesBefore()
    {
        var a = await NewDeviceAsync();
        var b = await NewDeviceAsync();
        await b.Db.ExecuteAsync("INSERT INTO SyncPeerState (DeviceId, LastSeq) VALUES ('ghost', 5)");

        await b.Engine.ApplySnapshotAsync(await a.Engine.CreateSnapshotAsync(), replaceLocalData: true);

        (await b.Db.Table<SyncPeerState>().ToListAsync()).Should().NotContain(p => p.DeviceId == "ghost");
    }

    [Fact]
    public async Task DeleteOfARowStillUsedHere_IsRefusedAtOnce_NoRowIsLeftPointingAtNothing()
    {
        var a = await NewDeviceAsync();
        var account = await a.AddAccountAsync("Cash 2");
        var category = await a.AddCategoryAsync("Test Food");
        var b = await PairedWithAsync(a);
        await a.DeleteAsync("Category", category);
        await Task.Delay(30);
        await b.AddTransactionAsync(await b.IdOfAsync("Category", "Test Food"), await b.IdOfAsync("Account", "Cash 2"), 500); // not sent yet
        await a.Engine.ExportAsync(a.Store);
        await SyncStores.CopyMissingAsync(a.Store, b.Store);

        await b.Engine.ApplyAsync(b.Store);

        (await b.DanglingReferencesAsync()).Should().Be(0, "B keeps the category its transaction uses, instead of waiting for A to bring it back");
        (await b.Db.Table<Category>().ToListAsync()).Should().Contain(c => c.Name == "Test Food");
    }

    [Fact]
    public async Task ExportThatStoppedAfterWritingItsFile_IsNotSentAgainWithOtherContent()
    {
        var a = await NewDeviceAsync();
        var b = await PairedWithAsync(a);
        await a.AddCategoryAsync("First");
        await a.CollectAsync();

        // The file is written and the device dies before recording it: simulate by writing it, then clearing the record
        await a.Engine.ExportAsync(a.Store);
        await a.Db.ExecuteAsync("UPDATE SyncMeta SET Dirty = 1");
        await a.Db.ExecuteAsync("DELETE FROM SyncPeerState WHERE DeviceId = ?", a.DeviceId);

        await a.AddCategoryAsync("Second");
        await SyncAllAsync(a, b);

        (await a.DescribeAsync()).Should().Be(await b.DescribeAsync());
        (await b.Db.Table<Category>().ToListAsync()).Select(c => c.Name).Should().Contain(["First", "Second"]);
    }

    [Fact]
    public async Task FileNeedingRowsThatHaveNotArrived_WaitsAndThenApplies()
    {
        var a = await NewDeviceAsync();
        var b = await PairedWithAsync(a);
        var c = await PairedWithAsync(a);

        var category = await a.AddCategoryAsync("Travel 2");
        await a.SyncThroughAsync(Shared);
        await b.SyncThroughAsync(Shared); // B now has A's category
        var onB = await b.Db.ExecuteScalarAsync<int>("SELECT Id FROM Category WHERE Name = 'Travel 2'");
        await b.AddTransactionAsync(onB, await b.Db.ExecuteScalarAsync<int>("SELECT MIN(Id) FROM Account"), 700);
        await b.Engine.ExportAsync(b.Store);

        // C is only given B's file: the category it refers to is in A's file, which has not come yet
        await c.Store.WriteAsync(b.DeviceId, 1, (await b.Store.ReadAsync(b.DeviceId, 1))!);
        (await c.Engine.ApplyAsync(c.Store)).Should().Be(0);
        (await c.DanglingReferencesAsync()).Should().Be(0);

        await c.Store.WriteAsync(a.DeviceId, 1, (await a.Store.ReadAsync(a.DeviceId, 1))!);
        (await c.Engine.ApplyAsync(c.Store)).Should().Be(2);
        (await c.Db.Table<Transaction>().ToListAsync()).Should().Contain(t => t.AmountCents == 700);
        category.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task FileFromANewerVersion_IsRefusedAndNothingIsApplied()
    {
        var a = await NewDeviceAsync();
        var newer = new SyncBatch(SyncEngine.FormatVersion, int.MaxValue, "newer-device", 1, []);
        await a.Store.WriteAsync("newer-device", 1, SyncCrypto.Seal(_key, "newer-device/00000001", JsonSerializer.SerializeToUtf8Bytes(newer)));

        var act = () => a.Engine.ApplyAsync(a.Store);

        await act.Should().ThrowAsync<SyncVersionException>();
    }

    [Fact]
    public async Task FileWithAnotherKey_IsRefused()
    {
        var a = await NewDeviceAsync();
        var batch = new SyncBatch(SyncEngine.FormatVersion, 1, "other", 1, []);
        await a.Store.WriteAsync("other", 1, SyncCrypto.Seal(SyncCrypto.NewKey(), "other/00000001", JsonSerializer.SerializeToUtf8Bytes(batch)));

        var act = () => a.Engine.ApplyAsync(a.Store);

        await act.Should().ThrowAsync<CryptographicException>();
    }

    [Fact]
    public async Task ChangeFiles_AreEncrypted()
    {
        var a = await NewDeviceAsync();
        await a.AddPayeeAsync("Secret Payee Name");
        await a.Engine.ExportAsync(a.Store);

        var bytes = await a.Store.ReadAsync(a.DeviceId, 1);

        System.Text.Encoding.UTF8.GetString(bytes!).Should().NotContain("Secret");
        ((Action)(() => SyncCrypto.Open(_key, "somewhere/else", bytes!))).Should().Throw<CryptographicException>("the file name is part of what is authenticated");
    }

    [Fact]
    public async Task RowsWithManyLinks_ArriveWithTheirLinksIntact()
    {
        var a = await NewDeviceAsync();
        var account = await a.AddAccountAsync("Pocket account");
        var category = await a.AddCategoryAsync("Pocket category");
        var transaction = await a.AddTransactionAsync(category, account, 4200);
        var payee = await a.AddPayeeAsync("Ana");
        await a.Db.InsertAsync(new Debt { PayeeId = payee, AccountId = account, Direction = DebtDirection.Lent });
        var pocket = new Pocket { AccountId = account, Name = "Trip money" };
        await a.Db.InsertAsync(pocket);
        await a.Db.InsertAsync(new PocketMovement { PocketId = pocket.Id, TransactionId = transaction, AmountCents = 4200, Date = new DateTime(2026, 11, 1) });
        var b = await PairedWithAsync(a);

        (await b.DescribeAsync()).Should().Be(await a.DescribeAsync());
        (await b.DanglingReferencesAsync()).Should().Be(0);
        (await b.Db.Table<PocketMovement>().CountAsync()).Should().Be(1);
    }

    /// <summary>The convergence criterion: seeded random operations on three devices, syncing at random moments.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public async Task RandomOperationsOnThreeDevices_ConvergeToTheSameState(int seed)
    {
        var rng = new Random(seed);
        var a = await NewDeviceAsync();
        var devices = new[] { a, await PairedWithAsync(a), await PairedWithAsync(a) };

        for (var step = 0; step < 150; step++)
        {
            _time.Now = _time.Now.AddMilliseconds(rng.Next(1, 4000));
            var d = devices[rng.Next(devices.Length)];
            var accounts = await d.IdsAsync("Account");
            var categories = await d.IdsAsync("Category");
            var transactions = await d.IdsAsync("Transaction");
            var payees = await d.IdsAsync("Payee");
            var transfers = await d.IdsAsync("Transfer");
            var budgets = await d.IdsAsync("Budget");
            int Pick(List<int> ids) => ids[rng.Next(ids.Count)];

            switch (rng.Next(100))
            {
                case < 6: await d.AddAccountAsync($"Account {step}"); break;
                case < 12: await d.AddCategoryAsync($"Category {step}"); break;
                case < 17: await d.AddPayeeAsync($"Payee {step}"); break;
                case < 45 when accounts.Count > 0 && categories.Count > 0:
                    await d.AddTransactionAsync(Pick(categories), Pick(accounts), rng.Next(1, 100000), payees.Count > 0 && rng.Next(2) == 0 ? Pick(payees) : null);
                    break;
                case < 50 when accounts.Count > 1: await d.AddTransferAsync(Pick(accounts), Pick(accounts), rng.Next(1, 5000)); break;
                case < 53 when categories.Count > 0: await d.AddBudgetAsync(Pick(categories), rng.Next(1000, 90000)); break;
                case < 65 when transactions.Count > 0: await d.UpdateAsync("Transaction", Pick(transactions), "AmountCents", (long)rng.Next(1, 100000)); break;
                case < 70 when categories.Count > 0: await d.UpdateAsync("Category", Pick(categories), "Name", $"Renamed {step}"); break;
                case < 78 when transactions.Count > 0: await d.DeleteAsync("Transaction", Pick(transactions)); break;
                case < 81 when transfers.Count > 0: await d.DeleteAsync("Transfer", Pick(transfers)); break;
                case < 84 when budgets.Count > 0: await d.DeleteAsync("Budget", Pick(budgets)); break;
                case < 87 when categories.Count > 0:
                    if (Pick(categories) is var cat && !await d.IsUsedAsync("Category", cat)) await d.DeleteAsync("Category", cat);
                    break;
                case < 89 when accounts.Count > 0:
                    if (Pick(accounts) is var acc && !await d.IsUsedAsync("Account", acc)) await d.DeleteAsync("Account", acc);
                    break;
                case < 91 when payees.Count > 0:
                    if (Pick(payees) is var pay && !await d.IsUsedAsync("Payee", pay)) await d.DeleteAsync("Payee", pay);
                    break;
                case >= 91:
                    if (rng.Next(4) > 0) await d.SyncThroughAsync(Shared); // sometimes a device stays behind
                    break;
            }
        }

        // Everyone comes back online
        await SyncAllAsync(devices);

        var expected = await devices[0].DescribeAsync();
        (await devices[1].DescribeAsync()).Should().Be(expected);
        (await devices[2].DescribeAsync()).Should().Be(expected);
        foreach (var d in devices)
        {
            (await d.DanglingReferencesAsync()).Should().Be(0);
            (await d.CollectAsync()).Should().Be(0, "nothing is left to send");
        }
    }
}
