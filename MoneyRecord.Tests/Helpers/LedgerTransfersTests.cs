using AwesomeAssertions;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using Xunit;

namespace MoneyRecord.Tests.Helpers;

public class LedgerTransfersTests
{
    private static readonly LedgerEntry Transfer = new()
    {
        Kind = LedgerEntryKind.Transfer, SourceId = 7, Date = new DateTime(2026, 3, 5), AmountCents = 20_000,
        CounterpartAmountCents = 20_000, Description = "rent", TransferNote = "rent", CurrencyCode = "EUR", CounterpartCurrencyCode = "EUR",
        AccountId = 1, AccountName = "Checking", CounterpartAccountId = 2, CounterpartAccountName = "Savings"
    };

    private static readonly LedgerEntry Expense = new()
    {
        Kind = LedgerEntryKind.Expense, SourceId = 3, AmountCents = 500, AccountId = 1, AccountName = "Checking"
    };

    [Fact]
    public void A_transfer_becomes_an_outgoing_and_an_incoming_half()
    {
        var entries = LedgerTransfers.PerAccount([Transfer]);

        entries.Should().HaveCount(2);
        entries.Should().AllSatisfy(e => e.SourceId.Should().Be(7));

        var outgoing = entries.Single(e => e.Kind == LedgerEntryKind.TransferOut);
        outgoing.AccountId.Should().Be(1);
        outgoing.CounterpartAccountId.Should().Be(2);
        outgoing.SignedAmount.Should().Be(-200m);
        outgoing.Description.Should().Be($"{string.Format(AppResources.TransferToAccount, "Savings")}: rent");

        var incoming = entries.Single(e => e.Kind == LedgerEntryKind.TransferIn);
        incoming.AccountId.Should().Be(2);
        incoming.AccountName.Should().Be("Savings");
        incoming.CounterpartAccountName.Should().Be("Checking");
        incoming.SignedAmount.Should().Be(200m);
        incoming.Description.Should().Be($"{string.Format(AppResources.TransferFromAccount, "Checking")}: rent");
    }

    [Fact]
    public void A_half_without_a_note_shows_only_the_other_account()
    {
        var unnamed = Transfer with { Description = "Checking → Savings", TransferNote = "" };

        var outgoing = LedgerTransfers.PerAccount([unnamed]).Single(e => e.Kind == LedgerEntryKind.TransferOut);

        outgoing.Description.Should().Be(string.Format(AppResources.TransferToAccount, "Savings"));
    }

    [Fact]
    public void Transactions_are_kept_as_they_are()
    {
        LedgerTransfers.PerAccount([Expense]).Should().Equal(Expense);
    }

    [Fact]
    public void A_cross_currency_transfer_shows_each_half_in_its_own_account_currency()
    {
        var abroad = Transfer with { CounterpartAmountCents = 21_500, CounterpartCurrencyCode = "USD" };

        var entries = LedgerTransfers.PerAccount([abroad]);

        var outgoing = entries.Single(e => e.Kind == LedgerEntryKind.TransferOut);
        (outgoing.SignedAmount, outgoing.CurrencyCode).Should().Be((-200m, "EUR"));
        var incoming = entries.Single(e => e.Kind == LedgerEntryKind.TransferIn);
        (incoming.SignedAmount, incoming.CurrencyCode).Should().Be((215m, "USD"));
        (incoming.CounterpartAmountCents, incoming.CounterpartCurrencyCode).Should().Be((20_000, "EUR"));
    }

    [Fact]
    public void A_whole_cross_currency_transfer_shows_what_left_and_what_arrived()
    {
        var abroad = Transfer with { CounterpartAmountCents = 21_500, CounterpartCurrencyCode = "USD" };

        abroad.IsCrossCurrency.Should().BeTrue();
        abroad.AmountText.Should().Be($"{MoneyFormatter.Format(200m, "EUR")}\n→ {MoneyFormatter.Format(215m, "USD")}");
        Transfer.IsCrossCurrency.Should().BeFalse();
        Transfer.AmountText.Should().Be(MoneyFormatter.Format(200m, "EUR"));
    }
}
