using AwesomeAssertions;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;
using NSubstitute;
using Xunit;

namespace MoneyRecord.Tests.Services;

public sealed class LedgerServiceTests : IDisposable
{
    private static readonly DateTime Start = new(2026, 3, 1);
    private static readonly DateTime End = new(2026, 3, 31);

    private readonly ITransactionEnrichmentService _enrichment = Substitute.For<ITransactionEnrichmentService>();
    private readonly ITransactionRepository _transactions = Substitute.For<ITransactionRepository>();
    private readonly ITransferRepository _transfers = Substitute.For<ITransferRepository>();
    private readonly AppPaths _paths = new(Path.Combine(Path.GetTempPath(), "mr-ledger-" + Guid.NewGuid().ToString("N")));
    private readonly LedgerService _sut;

    public LedgerServiceTests()
    {
        _sut = new LedgerService(_enrichment, _transactions, _transfers, _paths);
        _enrichment.GetEnrichedTransactionsAsync(Start, End).Returns(new List<Transaction>());
        _enrichment.GetEnrichedTransfersAsync(Start, End).Returns(new List<Transfer>());
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

    [Fact]
    public async Task Deleting_a_transaction_also_deletes_its_receipt()
    {
        Directory.CreateDirectory(_paths.ReceiptsFolder);
        var receipt = Path.Combine(_paths.ReceiptsFolder, "r.jpg");
        File.WriteAllText(receipt, "photo");
        var transaction = new Transaction { Id = 42, ReceiptPhotoPath = receipt };
        _transactions.GetByIdAsync(42).Returns(transaction);

        await _sut.DeleteAsync(new LedgerEntry { Kind = LedgerEntryKind.Expense, SourceId = 42 });

        await _transactions.Received(1).DeleteAsync(transaction);
        File.Exists(receipt).Should().BeFalse();
    }

    [Theory]
    [InlineData(LedgerEntryKind.Transfer)]
    [InlineData(LedgerEntryKind.TransferIn)]
    public async Task Deleting_either_half_deletes_the_whole_transfer(LedgerEntryKind kind)
    {
        var transfer = new Transfer { Id = 7 };
        _transfers.GetByIdAsync(7).Returns(transfer);

        await _sut.DeleteAsync(new LedgerEntry { Kind = kind, SourceId = 7 });

        await _transfers.Received(1).DeleteAsync(transfer);
        await _transactions.DidNotReceiveWithAnyArgs().DeleteAsync(default!);
    }

    [Fact]
    public async Task An_entry_that_is_already_gone_is_ignored()
    {
        await _sut.DeleteAsync(new LedgerEntry { Kind = LedgerEntryKind.Income, SourceId = 99 });

        await _transactions.DidNotReceiveWithAnyArgs().DeleteAsync(default!);
    }

    public void Dispose()
    {
        var root = Path.GetDirectoryName(_paths.ReceiptsFolder)!;
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }
}
