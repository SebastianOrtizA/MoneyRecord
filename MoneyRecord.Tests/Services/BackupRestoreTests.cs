using AwesomeAssertions;
using MoneyRecord.Models;
using MoneyRecord.Services;
using MoneyRecord.Services.Repositories;
using SQLite;
using System.IO.Compression;
using Xunit;

namespace MoneyRecord.Tests.Services;

/// <summary>
/// DataExportService backup (.zip) and restore against a real temp SQLite file and a temp app-data folder.
/// </summary>
public sealed class BackupRestoreTests : IAsyncLifetime
{
    private readonly string _appData = Path.Combine(Path.GetTempPath(), $"moneyrecord-backup-{Guid.NewGuid():N}");
    private DatabaseInitializer _dbInitializer = null!;
    private AccountRepository _accountRepository = null!;
    private CategoryRepository _categoryRepository = null!;
    private DataExportService _service = null!;

    private SQLiteAsyncConnection Db => _dbInitializer.Database!;
    private string Receipts => Path.Combine(_appData, "receipts");

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(Receipts);
        Directory.CreateDirectory(Path.Combine(_appData, "out"));
        _dbInitializer = new DatabaseInitializer(new AppPaths(_appData));
        await _dbInitializer.InitializeAsync();
        _accountRepository = new AccountRepository(_dbInitializer);
        _categoryRepository = new CategoryRepository(_dbInitializer);
        _service = new DataExportService(_dbInitializer, _accountRepository, _categoryRepository, new TransactionRepository(_dbInitializer), new AppPaths(_appData));
    }

    public async Task DisposeAsync()
    {
        await Db.CloseAsync();
        Directory.Delete(_appData, recursive: true);
    }

    [Fact]
    public async Task Backup_IsAZipWithDataAndReceipts_PathsRelative()
    {
        var receipt = WriteReceipt("receipt_1.jpg", "photo-1");
        await Db.InsertAsync(new Transaction { Description = "Lunch", Amount = 12.34m, ReceiptPhotoPath = receipt });
        var backupPath = BackupPath("backup.zip");

        await _service.ExportBackupAsync(backupPath);

        using var zip = ZipFile.OpenRead(backupPath);
        zip.Entries.Select(e => e.FullName).Should().BeEquivalentTo("data.json", "receipts/receipt_1.jpg");
        using var reader = new StreamReader(zip.GetEntry("data.json")!.Open());
        var json = await reader.ReadToEndAsync();
        json.Should().Contain("\"ReceiptPhotoPath\": \"receipts/receipt_1.jpg\"").And.Contain("\"Version\": 3");
        json.Should().NotContain(_appData.Replace("\\", "\\\\"), "no device paths in a backup");
    }

    [Fact]
    public async Task RoundTrip_RestoresRowsIdsAndReceipts()
    {
        var food = await AddCategoryAsync("Food");
        var bank = await AddAccountAsync("Bank");
        var cash = (await _accountRepository.GetDefaultAsync())!;
        await Db.InsertAsync(new Transaction { Description = "Lunch", Amount = 12.34m, CategoryId = food.Id, AccountId = bank.Id, ReceiptPhotoPath = WriteReceipt("receipt_1.jpg", "photo-1") });
        await Db.InsertAsync(new Transfer { Amount = 50m, SourceAccountId = cash.Id, DestinationAccountId = bank.Id });
        await Db.InsertAsync(new Budget { CategoryId = food.Id, LimitAmount = 300m });
        await Db.InsertAsync(new RecurringTransaction { Description = "Rent", Amount = 500m, CategoryId = food.Id, AccountId = bank.Id });
        var before = await DumpAsync();
        var backupPath = BackupPath("backup.zip");
        await _service.ExportBackupAsync(backupPath);

        // Change everything after the backup
        await Db.DeleteAllAsync<Transaction>();
        await Db.InsertAsync(new Transaction { Description = "Later", Amount = 1m, ReceiptPhotoPath = WriteReceipt("receipt_2.jpg", "photo-2") });
        File.Delete(Path.Combine(Receipts, "receipt_1.jpg"));
        await AddCategoryAsync("Later category");

        var result = await _service.RestoreAsync(backupPath);

        (await DumpAsync()).Should().BeEquivalentTo(before);
        result.RecordCount.Should().Be(before.Count);
        var lunch = await Db.Table<Transaction>().FirstAsync();
        lunch.ReceiptPhotoPath.Should().Be(Path.Combine(Receipts, "receipt_1.jpg"));
        File.ReadAllText(lunch.ReceiptPhotoPath!).Should().Be("photo-1");
        Directory.GetFiles(Receipts).Select(Path.GetFileName).Should().Equal("receipt_1.jpg");
        File.Exists(result.SnapshotPath).Should().BeTrue();
    }

    [Fact]
    public async Task Undo_RestoringTheSnapshot_BringsBackThePreviousData()
    {
        var backupPath = BackupPath("empty.zip");
        await _service.ExportBackupAsync(backupPath);
        await Db.InsertAsync(new Transaction { Description = "Keep me", Amount = 5m, ReceiptPhotoPath = WriteReceipt("receipt_3.jpg", "photo-3") });
        var before = await DumpAsync();

        var result = await _service.RestoreAsync(backupPath);
        (await Db.Table<Transaction>().CountAsync()).Should().Be(0);
        File.Exists(Path.Combine(Receipts, "receipt_3.jpg")).Should().BeFalse();

        await _service.RestoreAsync(result.SnapshotPath);

        (await DumpAsync()).Should().BeEquivalentTo(before);
        File.ReadAllText(Path.Combine(Receipts, "receipt_3.jpg")).Should().Be("photo-3");
    }

    [Fact]
    public async Task Restore_DuplicateCategoryNames_KeepsEachTransactionOnItsOwnCategory()
    {
        var first = await AddCategoryAsync("Food");
        var second = await AddCategoryAsync("Food");
        await Db.InsertAsync(new Transaction { Description = "Second", CategoryId = second.Id });
        await Db.InsertAsync(new Budget { CategoryId = second.Id, LimitAmount = 10m });
        var backupPath = BackupPath("backup.zip");
        await _service.ExportBackupAsync(backupPath);

        await _service.RestoreAsync(backupPath);

        (await Db.Table<Transaction>().FirstAsync()).CategoryId.Should().Be(second.Id).And.NotBe(first.Id);
        (await Db.Table<Budget>().FirstAsync()).CategoryId.Should().Be(second.Id);
    }

    [Fact]
    public async Task Restore_FailureMidway_LeavesDataAndReceiptsUntouched()
    {
        var backupPath = BackupPath("backup.zip");
        await Db.InsertAsync(new Budget { CategoryId = 1, LimitAmount = 10m });
        await _service.ExportBackupAsync(backupPath);
        await Db.DeleteAllAsync<Budget>();
        await Db.InsertAsync(new Transaction { Description = "Current", Amount = 7m, ReceiptPhotoPath = WriteReceipt("receipt_4.jpg", "photo-4") });
        var before = await DumpAsync();
        // Inserting the backup's budget fails after categories, accounts and transactions were already replaced
        await Db.ExecuteAsync("CREATE TRIGGER fail_budget BEFORE INSERT ON Budget BEGIN SELECT RAISE(ABORT, 'boom'); END");

        var act = () => _service.RestoreAsync(backupPath);

        await act.Should().ThrowAsync<SQLiteException>();
        (await DumpAsync()).Should().BeEquivalentTo(before);
        File.ReadAllText(Path.Combine(Receipts, "receipt_4.jpg")).Should().Be("photo-4");
        Directory.Exists(Path.Combine(_appData, "receipts.restoring")).Should().BeFalse();
    }

    [Fact]
    public async Task Restore_InvalidFile_ChangesNothing()
    {
        await Db.InsertAsync(new Transaction { Description = "Current" });
        var before = await DumpAsync();
        var notABackup = BackupPath("notes.zip");
        using (var zip = ZipFile.Open(notABackup, ZipArchiveMode.Create))
            zip.CreateEntry("notes.txt");

        var act = () => _service.RestoreAsync(notABackup);

        await act.Should().ThrowAsync<InvalidDataException>();
        (await DumpAsync()).Should().BeEquivalentTo(before);
    }

    [Fact]
    public async Task Restore_NewerFormat_IsRefused()
    {
        var path = BackupPath("future.json");
        await File.WriteAllTextAsync(path, """{ "Version": 99 }""");

        var act = () => _service.RestoreAsync(path);

        await act.Should().ThrowAsync<InvalidDataException>();
    }

    [Fact]
    public async Task Restore_LegacyJson_MapsCategoriesByIdAndKeepsExistingReceipts()
    {
        WriteReceipt("receipt_old.jpg", "old-photo");
        var orphan = WriteReceipt("receipt_current.jpg", "belongs to the replaced data");
        var path = BackupPath("legacy.json");
        await File.WriteAllTextAsync(path, """
            {
              "Version": 2,
              "Categories": [ { "Id": 10, "Name": "Food", "Type": 1 }, { "Id": 11, "Name": "Food", "Type": 1 } ],
              "Accounts": [ { "Id": 5, "Name": "Bank", "IsDefault": true } ],
              "Transactions": [
                { "Id": 1, "Description": "Lunch", "Amount": 12.34, "AmountCents": 1234, "CategoryId": 11, "AccountId": 5,
                  "ReceiptPhotoPath": "/data/user/0/com.moneyrecord/files/receipts/receipt_old.jpg" },
                { "Id": 2, "Description": "Lost photo", "Amount": 1, "CategoryId": 10, "AccountId": 5,
                  "ReceiptPhotoPath": "/data/user/0/com.moneyrecord/files/receipts/gone.jpg" }
              ]
            }
            """);

        var result = await _service.RestoreAsync(path);

        result.RecordCount.Should().Be(5);
        var lunch = await Db.FindAsync<Transaction>(1);
        lunch.CategoryId.Should().Be(11);
        lunch.AccountId.Should().Be(5);
        lunch.AmountCents.Should().Be(1234);
        lunch.ReceiptPhotoPath.Should().Be(Path.Combine(Receipts, "receipt_old.jpg"));
        (await Db.FindAsync<Transaction>(2)).ReceiptPhotoPath.Should().BeNull("the photo isn't on this device");
        File.Exists(orphan).Should().BeFalse("no restored transaction uses it (it is in the snapshot)");
        var bank = (await _accountRepository.GetAllAsync()).Should().ContainSingle().Which;
        bank.Name.Should().Be("Bank", "the repository cache is refreshed");
        bank.CurrencyCode.Should().Be(MoneyRecord.Helpers.MoneyFormatter.ResolveCurrencyCode(null),
            "an account from before currencies gets a fixed one, so changing the base currency can't relabel it");
    }

    [Fact]
    public async Task Restore_ZipEntriesCannotEscapeTheReceiptsFolder()
    {
        var path = BackupPath("evil.zip");
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            using (var writer = new StreamWriter(zip.CreateEntry("data.json").Open()))
                writer.Write("""{ "Version": 3 }""");
            using (var writer = new StreamWriter(zip.CreateEntry("receipts/../../evil.txt").Open()))
                writer.Write("x");
        }

        await _service.RestoreAsync(path);

        File.Exists(Path.Combine(_appData, "evil.txt")).Should().BeFalse();
        File.Exists(Path.Combine(Path.GetDirectoryName(_appData)!, "evil.txt")).Should().BeFalse();
        Directory.GetFiles(Receipts).Should().BeEmpty("no restored transaction uses it");
    }

    [Fact]
    public async Task Restore_KeepsOnlyTheLatestSnapshots()
    {
        var backupPath = BackupPath("backup.zip");
        await _service.ExportBackupAsync(backupPath);

        for (var i = 0; i < 7; i++)
            await _service.RestoreAsync(backupPath);

        Directory.GetFiles(_service.BackupsFolder, "pre-restore-*.zip").Should().HaveCount(5);
    }

    private string BackupPath(string name) => Path.Combine(_appData, "out", name);

    private string WriteReceipt(string name, string content)
    {
        var path = Path.Combine(Receipts, name);
        File.WriteAllText(path, content);
        return path;
    }

    private async Task<Category> AddCategoryAsync(string name)
    {
        var category = new Category { Name = name, Type = CategoryType.Expense };
        await Db.InsertAsync(category);
        return category;
    }

    private async Task<Account> AddAccountAsync(string name)
    {
        var account = new Account { Name = name, CurrencyCode = "USD" };
        await Db.InsertAsync(account);
        return account;
    }

    /// <summary>Every row of every table, as comparable records.</summary>
    private async Task<List<object>> DumpAsync()
    {
        var rows = new List<object>();
        rows.AddRange(await Db.Table<Category>().ToListAsync());
        rows.AddRange(await Db.Table<Account>().ToListAsync());
        rows.AddRange(await Db.Table<Transaction>().ToListAsync());
        rows.AddRange(await Db.Table<Transfer>().ToListAsync());
        rows.AddRange(await Db.Table<Budget>().ToListAsync());
        rows.AddRange(await Db.Table<RecurringTransaction>().ToListAsync());
        return rows;
    }
}
