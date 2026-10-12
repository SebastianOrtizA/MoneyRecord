using AwesomeAssertions;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using Xunit;

namespace MoneyRecord.Tests.Helpers;

public class LedgerGroupingTests
{
    private static LedgerEntry Expense(int id, string category, long cents, string currency = "EUR", int accountId = 1, string account = "Wallet") => new()
    {
        Kind = LedgerEntryKind.Expense, SourceId = id, AmountCents = cents, CurrencyCode = currency,
        CategoryId = id, CategoryName = category, CategoryIconCode = "F0001", AccountId = accountId, AccountName = account
    };

    [Fact]
    public void Category_groups_total_their_entries_and_are_sorted_by_name()
    {
        var groups = LedgerGrouping.Group([Expense(1, "Rent", 50_000), Expense(2, "Food", 1_000), Expense(3, "Food", 2_500)],
            GroupingMode.Category);

        groups.Select(g => g.GroupName).Should().Equal("Food", "Rent");
        groups[0].Total.Should().Be(35m);
        groups[0].Type.Should().Be(TransactionType.Expense);
        groups[0].TransactionCount.Should().Be(2);
        groups[0].GroupIconCode.Should().Be("F0001");
        groups[0].CurrencyCode.Should().Be("EUR");
    }

    [Fact]
    public void A_category_group_in_mixed_currencies_uses_the_base_currency()
    {
        var groups = LedgerGrouping.Group([Expense(1, "Food", 100, "EUR"), Expense(2, "Food", 100, "USD")], GroupingMode.Category);

        groups[0].CurrencyCode.Should().BeEmpty();
    }

    [Fact]
    public void A_transfer_group_totals_the_amount_moved()
    {
        var transfer = new LedgerEntry { Kind = LedgerEntryKind.Transfer, SourceId = 1, AmountCents = 10_000, CategoryName = "Transfers" };

        var group = LedgerGrouping.Group([transfer, transfer with { SourceId = 2 }], GroupingMode.Category).Single();

        group.Type.Should().Be(TransactionType.Transfer);
        group.Total.Should().Be(200m);
    }

    [Fact]
    public void Account_groups_show_the_account_balance_in_its_currency_even_with_a_transfer_from_another_currency()
    {
        var incoming = new LedgerEntry
        {
            Kind = LedgerEntryKind.TransferIn, SourceId = 1, AmountCents = 100, CurrencyCode = "USD",
            CategoryName = "Transfers", AccountId = 1, AccountName = "Wallet"
        };
        var balances = new List<AccountBalanceInfo> { new() { AccountId = 1, AccountName = "Wallet", CurrentBalance = -5m, CurrencyCode = "EUR" } };
        var accounts = new List<Account> { new() { Id = 1, Name = "Wallet", IconCode = "F0123" } };

        var group = LedgerGrouping.Group([Expense(1, "Food", 500), incoming], GroupingMode.Account, balances, accounts).Single();

        group.Total.Should().Be(-5m);
        group.Type.Should().Be(TransactionType.Expense);
        group.CurrencyCode.Should().Be("EUR");
        group.GroupIconCode.Should().Be("F0123");
        group.TransactionCount.Should().Be(2);
    }

    [Fact]
    public void Account_groups_with_the_same_name_stay_apart()
    {
        var groups = LedgerGrouping.Group([Expense(1, "Food", 100, accountId: 1, account: "Main"), Expense(2, "Food", 100, accountId: 2, account: "Main")],
            GroupingMode.Account);

        groups.Should().HaveCount(2);
        groups.Should().AllSatisfy(g => g.Total.Should().Be(0));
    }

    [Fact]
    public void No_grouping_gives_no_groups()
    {
        LedgerGrouping.Group([Expense(1, "Food", 100)], GroupingMode.None).Should().BeEmpty();
    }
}
