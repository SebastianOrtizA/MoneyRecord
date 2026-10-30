using AwesomeAssertions;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.Services.Repositories;
using NSubstitute;
using SQLite;
using Xunit;

namespace MoneyRecord.Tests.Services;

public sealed class LedgerServiceTests : IAsyncLifetime
{
    private static readonly DateTime Start = new(2026, 3, 1);
    private static readonly DateTime End = new(2026, 3, 31);

    private readonly ITransactionEnrichmentService _enrichment = Substitute.For<ITransactionEnrichmentService>();
    private readonly string _appData = Path.Combine(Path.GetTempPath(), "mr-ledger-" + Guid.NewGuid().ToString("N"));
    private AppPaths _paths = null!;
    private DatabaseInitializer _database = null!;
    private LedgerService _sut = null!;

    private SQLiteAsyncConnection Db { get; set; } = null!;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_appData);
        _paths = new AppPaths(_appData);
        _database = new DatabaseInitializer(_paths);
        Db = await _database.GetConnectionAsync();
        _sut = new LedgerService(_enrichment, _database, _paths);
        _enrichment.GetEnrichedTransactionsAsync(Start, End).Returns(new List<Transaction>());
        _enrichment.GetEnrichedTransfersAsync(Start, End).Returns(new List<Transfer>());
    }

    public async Task DisposeAsync()
    {
        await Db.CloseAsync();
        Directory.Delete(_appData, recursive: true);
    }

    private void GivenTransfer(string description = "rent") =>
        _enrichment.GetEnrichedTransfersAsync(Start, End).Returns(new List<Transfer>
        {
            new()
            {
                Id = 7, Date = new DateTime(2026, 3, 5), Amount = 200m, Description = description, CurrencyCode = "EUR",
                SourceAccountId = 1, SourceAccountName = "Checking", DestinationAccountId = 2, DestinationAccountName = "Savings"
            }
        });

    [Fact]
    public async Task A_transaction_keeps_its_id_names_and_amount()
    {
        _enrichment.GetEnrichedTransactionsAsync(Start, End).Returns(new List<Transaction>
        {
            new()
            {
                Id = 42, Type = TransactionType.Expense, Amount = 12.34m, Description = "Lunch", CategoryId = 3, CategoryName = "Food",
                AccountId = 1, AccountName = "Checking", CurrencyCode = "EUR", Tags = "work", ReceiptPhotoPath = "r.jpg"
            }
        });

        var entry = (await _sut.GetEntriesAsync(Start, End)).Single();

        entry.Kind.Should().Be(LedgerEntryKind.Expense);
        entry.SourceId.Should().Be(42);
        entry.AmountCents.Should().Be(1_234);
        entry.SignedAmount.Should().Be(-12.34m);
        entry.CategoryId.Should().Be(3);
        entry.AccountName.Should().Be("Checking");
        entry.HasTags.Should().BeTrue();
        entry.HasReceipt.Should().BeTrue();
    }

    [Fact]
    public async Task A_transfer_is_one_entry_with_its_real_id()
    {
        GivenTransfer();

        var entry = (await _sut.GetEntriesAsync(Start, End)).Single();

        entry.Kind.Should().Be(LedgerEntryKind.Transfer);
        entry.SourceId.Should().Be(7);
        entry.Description.Should().Be("rent");
        entry.TransferNote.Should().Be("rent");
        entry.AccountId.Should().Be(1);
        entry.CounterpartAccountId.Should().Be(2);
        entry.AccountDisplayName.Should().Be("Checking → Savings");
        entry.CategoryId.Should().BeNull();
        entry.CategoryName.Should().Be(AppResources.Transfers);
        entry.SignedAmount.Should().Be(200m);
        entry.CurrencyCode.Should().Be("EUR");
    }

    [Fact]
    public async Task A_transfer_without_description_shows_its_accounts()
    {
        GivenTransfer(description: "");

        var entry = (await _sut.GetEntriesAsync(Start, End)).Single();

        entry.Description.Should().Be("Checking → Savings");
        entry.TransferNote.Should().BeEmpty();
    }

    private async Task<(Account Account, Category Category)> SeedAsync() =>
        (await Db.Table<Account>().FirstAsync(), await Db.Table<Category>().FirstAsync());

    private async Task<Account> AddAccountAsync(string name)
    {
        var account = new Account { Name = name };
        await Db.InsertAsync(account);
        return account;
    }

    private string WriteReceipt(string name)
    {
        Directory.CreateDirectory(_paths.ReceiptsFolder);
        var receipt = Path.Combine(_paths.ReceiptsFolder, name);
        File.WriteAllText(receipt, "photo");
        return receipt;
    }

    private static LedgerEntry EntryOf(Transaction t) => new()
    {
        Kind = t.Type == TransactionType.Income ? LedgerEntryKind.Income : LedgerEntryKind.Expense, SourceId = t.Id, SplitId = t.SplitId
    };

    [Fact]
    public async Task A_deleted_transaction_comes_back_with_its_id_and_fields()
    {
        var (account, category) = await SeedAsync();
        var transaction = new Transaction
        {
            Type = TransactionType.Expense, Amount = 12.34m, Description = "Lunch", Date = new DateTime(2026, 3, 5), CategoryId = category.Id,
            AccountId = account.Id, Tags = "work"
        };
        await Db.InsertAsync(transaction);

        var deleted = await _sut.DeleteAsync(EntryOf(transaction));

        deleted.Should().NotBeNull();
        (await Db.Table<Transaction>().CountAsync()).Should().Be(0);
        (await _sut.RestoreAsync(deleted!)).Should().BeTrue();
        var back = await Db.FindAsync<Transaction>(transaction.Id);
        back.Should().NotBeNull();
        (back!.AmountCents, back.Description, back.Date, back.Tags, back.CategoryId, back.AccountId)
            .Should().Be((1_234, "Lunch", new DateTime(2026, 3, 5), "work", category.Id, account.Id));
    }

    [Fact]
    public async Task A_deleted_transfer_comes_back_with_both_amounts()
    {
        var (account, _) = await SeedAsync();
        var other = await AddAccountAsync("Savings");
        var transfer = new Transfer
        {
            Amount = 200m, DestinationAmountCents = 21_000, SourceAccountId = account.Id, DestinationAccountId = other.Id, Description = "rent"
        };
        await Db.InsertAsync(transfer);

        var deleted = await _sut.DeleteAsync(new LedgerEntry { Kind = LedgerEntryKind.TransferIn, SourceId = transfer.Id });

        (await Db.Table<Transfer>().CountAsync()).Should().Be(0);
        (await _sut.RestoreAsync(deleted!)).Should().BeTrue();
        var back = await Db.FindAsync<Transfer>(transfer.Id);
        (back!.AmountCents, back.DestinationAmountCents, back.Description).Should().Be((20_000, 21_000, "rent"));
    }

    [Fact]
    public async Task A_deleted_split_goes_and_comes_back_as_a_whole()
    {
        var (account, category) = await SeedAsync();
        var parts = new[] { 5m, 7m, 9m }
            .Select(a => new Transaction { Type = TransactionType.Expense, Amount = a, CategoryId = category.Id, AccountId = account.Id, SplitId = "s1" })
            .ToList();
        await Db.InsertAllAsync(parts);
        var ids = parts.Select(p => p.Id).ToList();

        var deleted = await _sut.DeleteAsync(EntryOf(parts[1]));

        deleted!.Transactions.Should().HaveCount(3);
        (await Db.Table<Transaction>().CountAsync()).Should().Be(0);
        (await _sut.RestoreAsync(deleted)).Should().BeTrue();
        (await Db.Table<Transaction>().OrderBy(t => t.Id).ToListAsync()).Select(t => t.Id).Should().Equal(ids);
    }

    [Fact]
    public async Task A_pocket_take_out_still_points_at_a_restored_transaction()
    {
        var (account, category) = await SeedAsync();
        var transaction = new Transaction { Type = TransactionType.Expense, Amount = 40m, CategoryId = category.Id, AccountId = account.Id };
        await Db.InsertAsync(transaction);
        var pocket = new Pocket { AccountId = account.Id, Name = "Holiday" };
        await Db.InsertAsync(pocket);
        await Db.InsertAsync(new PocketMovement { PocketId = pocket.Id, AmountCents = -2_000, TransactionId = transaction.Id });

        var deleted = await _sut.DeleteAsync(EntryOf(transaction));
        await _sut.RestoreAsync(deleted!);

        var movement = await Db.Table<PocketMovement>().FirstAsync();
        movement.TransactionId.Should().Be(transaction.Id);
        (await Db.FindAsync<Transaction>(movement.TransactionId!.Value)).Should().NotBeNull();
    }

    [Fact]
    public async Task Deleting_keeps_the_receipt_until_it_is_discarded_and_a_restore_keeps_it()
    {
        var (account, category) = await SeedAsync();
        var receipt = WriteReceipt("r.jpg");
        var transaction = new Transaction { Type = TransactionType.Expense, CategoryId = category.Id, AccountId = account.Id, ReceiptPhotoPath = receipt };
        await Db.InsertAsync(transaction);

        var deleted = await _sut.DeleteAsync(EntryOf(transaction));
        File.Exists(receipt).Should().BeTrue("an undo needs the photo");
        await _sut.RestoreAsync(deleted!);
        File.Exists(receipt).Should().BeTrue();

        var again = await _sut.DeleteAsync(EntryOf(transaction));
        _sut.Discard(again!);
        File.Exists(receipt).Should().BeFalse();
    }

    [Fact]
    public async Task A_split_shares_its_receipt_and_discarding_it_deletes_it_once()
    {
        var (account, category) = await SeedAsync();
        var receipt = WriteReceipt("r.jpg");
        var parts = new[] { 1m, 2m }
            .Select(a => new Transaction { Amount = a, CategoryId = category.Id, AccountId = account.Id, SplitId = "s1", ReceiptPhotoPath = receipt })
            .ToList();
        await Db.InsertAllAsync(parts);

        var deleted = await _sut.DeleteAsync(EntryOf(parts[0]));

        deleted!.ReceiptPaths.Should().Equal(receipt);
        _sut.Discard(deleted);
        File.Exists(receipt).Should().BeFalse();
    }

    [Fact]
    public async Task A_restore_is_refused_when_the_id_is_taken_again()
    {
        var (account, category) = await SeedAsync();
        var transaction = new Transaction { Description = "old", CategoryId = category.Id, AccountId = account.Id };
        await Db.InsertAsync(transaction);
        var deleted = await _sut.DeleteAsync(EntryOf(transaction));
        // sqlite-net ignores a preset AutoIncrement ID unless it is written with "OR REPLACE" (what a backup restore does)
        await Db.InsertAsync(new Transaction { Id = transaction.Id, Description = "new", CategoryId = category.Id, AccountId = account.Id }, "OR REPLACE");

        (await _sut.RestoreAsync(deleted!)).Should().BeFalse();

        (await Db.Table<Transaction>().ToListAsync()).Should().ContainSingle().Which.Description.Should().Be("new");
    }

    [Fact]
    public async Task A_restore_is_refused_when_its_account_is_gone_and_writes_nothing()
    {
        var (_, category) = await SeedAsync();
        var bank = await AddAccountAsync("Bank");
        var transaction = new Transaction { CategoryId = category.Id, AccountId = bank.Id };
        await Db.InsertAsync(transaction);
        var deleted = await _sut.DeleteAsync(EntryOf(transaction));
        await Db.DeleteAsync<Account>(bank.Id);

        (await _sut.RestoreAsync(deleted!)).Should().BeFalse();

        (await Db.Table<Transaction>().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task An_entry_that_is_already_gone_returns_null()
    {
        (await _sut.DeleteAsync(new LedgerEntry { Kind = LedgerEntryKind.Income, SourceId = 99 })).Should().BeNull();
        (await _sut.DeleteAsync(new LedgerEntry { Kind = LedgerEntryKind.Transfer, SourceId = 99 })).Should().BeNull();
    }
}
