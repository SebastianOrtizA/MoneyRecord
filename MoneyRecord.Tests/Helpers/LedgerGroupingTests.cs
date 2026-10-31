using AwesomeAssertions;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using Xunit;

namespace MoneyRecord.Tests.Helpers;

public class LedgerGroupingTests
{
    // One category ID per category name
    private static LedgerEntry Expense(int id, string category, long cents, string currency = "EUR", int accountId = 1, string account = "Wallet") => new()
    {
        Kind = LedgerEntryKind.Expense, SourceId = id, AmountCents = cents, CurrencyCode = currency,
        CategoryId = category.GetHashCode(), CategoryName = category, CategoryIconCode = "F0001", AccountId = accountId, AccountName = account
    };

    private static LedgerEntry On(DateTime date, LedgerEntry entry) => entry with { Date = date };

    private static LedgerEntry Income(int id, long cents, string currency = "EUR") => new()
    {
        Kind = LedgerEntryKind.Income, SourceId = id, AmountCents = cents, CurrencyCode = currency, CategoryName = "Salary"
    };

    [Fact]
    public void Day_groups_hold_one_date_each_open_and_newest_first()
    {
        var groups = LedgerGrouping.Group([
            On(new DateTime(2026, 10, 29, 9, 0, 0), Expense(1, "Food", 1_000)),
            On(new DateTime(2026, 10, 31, 18, 0, 0), Expense(2, "Food", 2_000)),
            On(new DateTime(2026, 10, 31, 8, 0, 0), Expense(3, "Rent", 500))], GroupingMode.Date);

        groups.Select(g => g.TransactionCount).Should().Equal(2, 1);
        groups.Should().OnlyContain(g => g.IsExpanded);
        groups[0].GroupName.Should().Be(DateFormatter.Format(new DateTime(2026, 10, 31), DateStyle.Long));
    }

    [Fact]
    public void Day_groups_can_run_oldest_first()
    {
        var groups = LedgerGrouping.Group([
            On(new DateTime(2026, 10, 31), Expense(1, "Food", 1_000)),
            On(new DateTime(2026, 10, 29), Expense(2, "Food", 2_000))], GroupingMode.Date, ascending: true);

        groups.Select(g => g.TransactionCount).Should().Equal(1, 1);
        groups[0].GroupName.Should().Be(DateFormatter.Format(new DateTime(2026, 10, 29), DateStyle.Long));
    }

    [Fact]
    public void A_day_totals_its_net_per_currency_and_never_adds_currencies()
    {
        var day = new DateTime(2026, 10, 31);
        var group = LedgerGrouping.Group([
            On(day, Income(1, 10_000, "ZZA")), On(day, Expense(2, "Food", 2_500, "ZZA")), On(day, Expense(3, "Food", 1_000, "ZZB"))],
            GroupingMode.Date).Single();

        // Two non-base currencies, so the order does not depend on the machine's base currency
        group.Totals.Should().Equal(new CurrencyAmount("ZZA", 75m), new CurrencyAmount("ZZB", -10m));
    }

    [Fact]
    public void A_day_is_coloured_by_its_sign_only_when_every_currency_agrees()
    {
        var day = new DateTime(2026, 10, 31);

        LedgerGrouping.Group([On(day, Expense(1, "Food", 1_000, "ZZA")), On(day, Expense(2, "Food", 500, "ZZB"))], GroupingMode.Date)
            .Single().Type.Should().Be(TransactionType.Expense);
        LedgerGrouping.Group([On(day, Income(1, 1_000, "ZZA")), On(day, Income(2, 500, "ZZB"))], GroupingMode.Date)
            .Single().Type.Should().Be(TransactionType.Income);
        LedgerGrouping.Group([On(day, Income(1, 10_000, "ZZA")), On(day, Expense(2, "Food", 500, "ZZB"))], GroupingMode.Date)
            .Single().Type.Should().Be(TransactionType.Transfer, "mixed signs get the neutral colour, not whichever currency sorts first");
    }

    [Fact]
    public void A_transfer_moves_money_between_own_accounts_so_it_is_left_out_of_the_day_total()
    {
        var day = new DateTime(2026, 10, 31);
        var transfer = new LedgerEntry { Kind = LedgerEntryKind.Transfer, SourceId = 9, AmountCents = 30_000, CurrencyCode = "EUR", Date = day };

        var group = LedgerGrouping.Group([transfer, On(day, Expense(1, "Food", 1_000))], GroupingMode.Date).Single();

        group.TransactionCount.Should().Be(2);
        group.Totals.Should().Equal(new CurrencyAmount("EUR", -10m));

        LedgerGrouping.Group([transfer], GroupingMode.Date).Single().Totals.Should().BeEmpty();
    }

    [Fact]
    public void Category_groups_total_their_entries_and_are_sorted_by_name()
    {
        var groups = LedgerGrouping.Group([Expense(1, "Rent", 50_000), Expense(2, "Food", 1_000), Expense(3, "Food", 2_500)],
            GroupingMode.Category);

        groups.Select(g => g.GroupName).Should().Equal("Food", "Rent");
        groups[0].Totals.Should().Equal(new CurrencyAmount("EUR", 35m));
        groups[0].Type.Should().Be(TransactionType.Expense);
        groups[0].TransactionCount.Should().Be(2);
        groups[0].GroupIconCode.Should().Be("F0001");
    }

    [Fact]
    public void A_category_group_in_mixed_currencies_has_one_total_per_currency()
    {
        var groups = LedgerGrouping.Group([Expense(1, "Food", 100, "EUR"), Expense(2, "Food", 100, "USD")], GroupingMode.Category);

        groups[0].Totals.Select(t => t.CurrencyCode).Should().BeEquivalentTo(["EUR", "USD"]);
        groups[0].TotalText.Should().Contain(MoneyFormatter.Format(-1m, "EUR")).And.Contain(MoneyFormatter.Format(-1m, "USD"));
    }

    [Fact]
    public void A_transfer_group_totals_the_amount_moved()
    {
        var transfer = new LedgerEntry { Kind = LedgerEntryKind.Transfer, SourceId = 1, AmountCents = 10_000, CategoryName = "Transfers" };

        var group = LedgerGrouping.Group([transfer, transfer with { SourceId = 2 }], GroupingMode.Category).Single();

        group.Type.Should().Be(TransactionType.Transfer);
        group.Totals.Should().Equal(new CurrencyAmount(MoneyFormatter.BaseCurrency, 200m));
    }

    [Fact]
    public void Same_named_income_and_expense_categories_stay_apart()
    {
        var giftReceived = new LedgerEntry
        {
            Kind = LedgerEntryKind.Income, SourceId = 9, AmountCents = 5_000, CategoryId = 7, CategoryName = "Gifts"
        };

        var groups = LedgerGrouping.Group([Expense(1, "Gifts", 2_000), giftReceived], GroupingMode.Category);

        groups.Should().HaveCount(2);
        groups.Should().ContainSingle(g => g.Type == TransactionType.Expense && g.Totals.Single().Amount == 20m);
        groups.Should().ContainSingle(g => g.Type == TransactionType.Income && g.Totals.Single().Amount == 50m);
    }

    [Fact]
    public void A_user_category_named_like_the_transfers_group_is_not_mixed_with_transfers()
    {
        var transfer = new LedgerEntry { Kind = LedgerEntryKind.Transfer, SourceId = 1, AmountCents = 10_000, CategoryName = "Transfers" };

        var groups = LedgerGrouping.Group([transfer, Expense(1, "Transfers", 300)], GroupingMode.Category);

        groups.Select(g => g.Type).Should().BeEquivalentTo([TransactionType.Transfer, TransactionType.Expense]);
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

        group.Totals.Should().Equal(new CurrencyAmount("EUR", -5m));
        group.Type.Should().Be(TransactionType.Expense);
        group.GroupIconCode.Should().Be("F0123");
        group.TransactionCount.Should().Be(2);
    }

    [Fact]
    public void Account_groups_with_the_same_name_stay_apart()
    {
        var groups = LedgerGrouping.Group([Expense(1, "Food", 100, accountId: 1, account: "Main"), Expense(2, "Food", 100, accountId: 2, account: "Main")],
            GroupingMode.Account);

        groups.Should().HaveCount(2);
        groups.Should().AllSatisfy(g => g.Totals.Single().Amount.Should().Be(0));
    }

    [Fact]
    public void No_grouping_gives_no_groups()
    {
        LedgerGrouping.Group([Expense(1, "Food", 100)], GroupingMode.None).Should().BeEmpty();
    }
}
