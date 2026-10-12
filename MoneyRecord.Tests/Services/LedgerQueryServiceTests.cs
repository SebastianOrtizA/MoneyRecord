using AwesomeAssertions;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;
using NSubstitute;
using Xunit;

namespace MoneyRecord.Tests.Services;

public class LedgerQueryServiceTests
{
    private static readonly DateTime Start = new(2026, 3, 1);
    private static readonly DateTime End = new(2026, 3, 31);

    private readonly ITransactionEnrichmentService _enrichment = Substitute.For<ITransactionEnrichmentService>();
    private readonly LedgerQueryService _sut;

    public LedgerQueryServiceTests()
    {
        _sut = new LedgerQueryService(_enrichment);
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

        var entry = (await _sut.GetEntriesAsync(Start, End, transfersPerAccount: false)).Single();

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

        var entry = (await _sut.GetEntriesAsync(Start, End, transfersPerAccount: false)).Single();

        entry.Kind.Should().Be(LedgerEntryKind.Transfer);
        entry.SourceId.Should().Be(7);
        entry.Description.Should().Be("rent");
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

        var entry = (await _sut.GetEntriesAsync(Start, End, transfersPerAccount: false)).Single();

        entry.Description.Should().Be("Checking → Savings");
    }

    [Fact]
    public async Task Per_account_a_transfer_is_an_outgoing_and_an_incoming_half()
    {
        GivenTransfer();

        var entries = await _sut.GetEntriesAsync(Start, End, transfersPerAccount: true);

        entries.Should().HaveCount(2);
        entries.Should().AllSatisfy(e => e.SourceId.Should().Be(7));

        var outgoing = entries.Single(e => e.Kind == LedgerEntryKind.TransferOut);
        outgoing.AccountId.Should().Be(1);
        outgoing.SignedAmount.Should().Be(-200m);
        outgoing.Description.Should().Be($"{string.Format(AppResources.TransferToAccount, "Savings")}: rent");

        var incoming = entries.Single(e => e.Kind == LedgerEntryKind.TransferIn);
        incoming.AccountId.Should().Be(2);
        incoming.AccountName.Should().Be("Savings");
        incoming.CounterpartAccountName.Should().Be("Checking");
        incoming.SignedAmount.Should().Be(200m);
        incoming.Description.Should().Be($"{string.Format(AppResources.TransferFromAccount, "Checking")}: rent");
    }
}
