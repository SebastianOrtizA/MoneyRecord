using AwesomeAssertions;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.Services.Repositories;
using NSubstitute;
using SQLite;
using Xunit;

namespace MoneyRecord.Tests.Services;

/// <summary>Several photos and PDFs per transaction: saving, the first photo as the primary, undo, backup and the old single-photo data.</summary>
public sealed class TransactionAttachmentTests : IAsyncLifetime
{
    private readonly string _appData = Path.Combine(Path.GetTempPath(), "mr-attach-" + Guid.NewGuid().ToString("N"));
    private AppPaths _paths = null!;
    private DatabaseInitializer _database = null!;
    private TransactionRepository _transactions = null!;
    private DataExportService _export = null!;
    private LedgerService _ledger = null!;

    private SQLiteAsyncConnection Db { get; set; } = null!;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(Path.Combine(_appData, "receipts"));
        _paths = new AppPaths(_appData);
        _database = new DatabaseInitializer(_paths);
        Db = await _database.GetConnectionAsync();
        _transactions = new TransactionRepository(_database);
        var accounts = new AccountRepository(_database);
        var categories = new CategoryRepository(_database);
        _export = new DataExportService(_database, accounts, categories, _transactions, new TransferRepository(_database),
            new PayeeRepository(_database), _paths, new BackupHistory(new FakePreferences(), TimeProvider.System));
        _ledger = new LedgerService(Substitute.For<ITransactionEnrichmentService>(), _database, _paths);
    }

    public async Task DisposeAsync()
    {
        await Db.CloseAsync();
        Directory.Delete(_appData, recursive: true);
    }

    private string WriteFile(string name)
    {
        var path = Path.Combine(_paths.ReceiptsFolder, name);
        File.WriteAllText(path, name);
        return path;
    }

    private TransactionAttachment Attach(string name) => new()
    {
        Path = WriteFile(name), Name = name, Kind = TransactionAttachments.KindOf(name), AddedDate = new DateTime(2026, 3, 5)
    };

    private async Task<Transaction> SaveWithAsync(params TransactionAttachment[] attachments)
    {
        var category = new Category { Name = "Food" };
        await Db.InsertAsync(category);
        var transaction = new Transaction { Amount = 12m, CategoryId = category.Id, Date = new DateTime(2026, 3, 5), PendingAttachments = attachments.ToList() };
        await _transactions.SaveAsync(transaction);
        return transaction;
    }

    [Fact]
    public async Task Saving_stores_every_attachment_and_the_first_photo_is_the_receipt()
    {
        var transaction = await SaveWithAsync(Attach("invoice.pdf"), Attach("a.jpg"), Attach("b.jpg"));

        (await _transactions.GetAttachmentsAsync(transaction.Id)).Select(a => a.Name).Should().Equal("invoice.pdf", "a.jpg", "b.jpg");
        (await Db.FindAsync<Transaction>(transaction.Id)).ReceiptPhotoPath.Should().Be(Path.Combine(_paths.ReceiptsFolder, "a.jpg"));
    }

    [Fact]
    public async Task Saving_without_pending_attachments_keeps_the_stored_ones()
    {
        var transaction = await SaveWithAsync(Attach("a.jpg"));
        var stored = await Db.FindAsync<Transaction>(transaction.Id);
        stored.Description = "edited";

        await _transactions.SaveAsync(stored);

        (await _transactions.GetAttachmentsAsync(transaction.Id)).Should().ContainSingle();
    }

    [Fact]
    public async Task Saving_an_empty_list_removes_them_and_clears_the_receipt()
    {
        var transaction = await SaveWithAsync(Attach("a.jpg"));
        var stored = await Db.FindAsync<Transaction>(transaction.Id);
        stored.PendingAttachments = [];

        await _transactions.SaveAsync(stored);

        (await _transactions.GetAttachmentsAsync(transaction.Id)).Should().BeEmpty();
        (await Db.FindAsync<Transaction>(transaction.Id)).ReceiptPhotoPath.Should().BeNull();
    }

    [Fact]
    public async Task A_deleted_transaction_comes_back_with_its_attachments_and_discard_removes_the_files()
    {
        var transaction = await SaveWithAsync(Attach("a.jpg"), Attach("invoice.pdf"));
        var entry = new LedgerEntry { Kind = LedgerEntryKind.Expense, SourceId = transaction.Id };

        var deleted = await _ledger.DeleteAsync(entry);

        (await Db.Table<TransactionAttachment>().CountAsync()).Should().Be(0);
        File.Exists(Path.Combine(_paths.ReceiptsFolder, "invoice.pdf")).Should().BeTrue("an undo needs it");
        (await _ledger.RestoreAsync(deleted!)).Should().BeTrue();
        (await _transactions.GetAttachmentsAsync(transaction.Id)).Select(a => a.Name).Should().Equal("a.jpg", "invoice.pdf");

        var again = await _ledger.DeleteAsync(entry);
        _ledger.Discard(again!);
        Directory.GetFiles(_paths.ReceiptsFolder).Should().BeEmpty();
    }

    [Fact]
    public async Task The_migration_gives_an_old_receipt_photo_a_row_and_keeps_the_file()
    {
        var path = WriteFile("old.jpg");
        var old = new Transaction { Amount = 5m, ReceiptPhotoPath = path, Date = new DateTime(2026, 1, 2) };
        await Db.InsertAsync(old);
        await Db.ExecuteAsync("DELETE FROM \"TransactionAttachment\"");

        await Db.RunInTransactionAsync(db => new MoneyRecord.Services.Repositories.Migrations.M006_TransactionAttachments().Up(db));

        var rows = await _transactions.GetAttachmentsAsync(old.Id);
        rows.Should().ContainSingle().Which.Should().Match<TransactionAttachment>(a => a.Path == path && a.Kind == AttachmentKind.Photo);
        File.Exists(path).Should().BeTrue();
    }

    [Fact]
    public async Task A_backup_round_trip_restores_two_photos_and_a_pdf()
    {
        var transaction = await SaveWithAsync(Attach("a.jpg"), Attach("invoice.pdf"), Attach("b.jpg"));
        var zip = Path.Combine(_appData, "out.zip");
        await _export.ExportBackupAsync(zip);
        File.Delete(Path.Combine(_paths.ReceiptsFolder, "invoice.pdf"));
        await Db.ExecuteAsync("DELETE FROM \"TransactionAttachment\"");

        await _export.RestoreAsync(zip);

        var rows = await _transactions.GetAttachmentsAsync(transaction.Id);
        rows.Select(a => a.Name).Should().Equal("a.jpg", "invoice.pdf", "b.jpg");
        rows.Select(a => a.Kind).Should().Equal(AttachmentKind.Photo, AttachmentKind.Pdf, AttachmentKind.Photo);
        rows.Should().OnlyContain(a => File.Exists(a.Path));
        File.ReadAllText(rows[1].Path).Should().Be("invoice.pdf");
    }

    [Fact]
    public async Task A_backup_without_attachment_rows_restores_the_old_single_photo()
    {
        var path = WriteFile("old.jpg");
        var old = new Transaction { Amount = 5m, ReceiptPhotoPath = path, Date = new DateTime(2026, 1, 2) };
        await Db.InsertAsync(old);
        await Db.ExecuteAsync("DELETE FROM \"TransactionAttachment\"");
        var zip = Path.Combine(_appData, "old.zip");
        await _export.ExportBackupAsync(zip);

        await _export.RestoreAsync(zip);

        (await _transactions.GetAttachmentsAsync(old.Id)).Should().ContainSingle().Which.Path.Should().Be(path);
    }
}
