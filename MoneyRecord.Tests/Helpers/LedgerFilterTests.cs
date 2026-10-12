using AwesomeAssertions;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using Xunit;

namespace MoneyRecord.Tests.Helpers;

public class LedgerFilterTests
{
    private static readonly LedgerEntry Lunch = new()
    {
        Kind = LedgerEntryKind.Expense, SourceId = 1, Date = new DateTime(2026, 3, 2), Description = "Lunch",
        AmountCents = 1_250, CategoryId = 10, CategoryName = "Food", AccountId = 1, AccountName = "Cash", Tags = "work"
    };

    private static readonly LedgerEntry Salary = new()
    {
        Kind = LedgerEntryKind.Income, SourceId = 2, Date = new DateTime(2026, 3, 1), Description = "March",
        AmountCents = 300_000, CategoryId = 20, CategoryName = "Salary", AccountId = 2, AccountName = "Bank"
    };

    private static readonly LedgerEntry Savings = new()
    {
        Kind = LedgerEntryKind.Transfer, SourceId = 1, Date = new DateTime(2026, 3, 3), Description = "Bank → Savings",
        AmountCents = 50_000, CategoryName = "Transfers", AccountId = 2, AccountName = "Bank",
        CounterpartAccountId = 3, CounterpartAccountName = "Savings"
    };

    private static readonly List<LedgerEntry> All = [Lunch, Salary, Savings];

    [Fact]
    public void No_filter_keeps_everything_newest_first()
    {
        LedgerFilter.Apply(All, new TransactionFilter()).Should().Equal(Savings, Lunch, Salary);
    }

    [Fact]
    public void Ascending_sorts_oldest_first()
    {
        LedgerFilter.Apply(All, new TransactionFilter(Ascending: true)).Should().Equal(Salary, Lunch, Savings);
    }

    [Theory]
    [InlineData("lunch")] // description
    [InlineData("FOOD")]  // category
    [InlineData("cash")]  // account
    [InlineData("work")]  // tag
    public void Search_matches_description_category_account_and_tags(string search)
    {
        LedgerFilter.Apply(All, new TransactionFilter(SearchText: search)).Should().Equal(Lunch);
    }

    [Fact]
    public void Search_finds_a_transfer_by_its_destination_account()
    {
        LedgerFilter.Apply(All, new TransactionFilter(SearchText: "savings")).Should().Equal(Savings);
    }

    [Fact]
    public void Type_filter_compares_the_transaction_type()
    {
        LedgerFilter.Apply(All, new TransactionFilter(Type: TransactionType.Transfer)).Should().Equal(Savings);
        LedgerFilter.Apply(All, new TransactionFilter(Type: TransactionType.Income)).Should().Equal(Salary);
    }

    [Fact]
    public void Category_filter_matches_by_id_not_name()
    {
        var renamedFood = Lunch with { SourceId = 9, CategoryName = "Salary" };

        LedgerFilter.Apply([Salary, renamedFood], new TransactionFilter(CategoryId: 20)).Should().Equal(Salary);
    }

    [Fact]
    public void Account_filter_includes_transfers_from_and_to_the_account()
    {
        LedgerFilter.Apply(All, new TransactionFilter(AccountId: 2)).Should().Equal(Savings, Salary);
        LedgerFilter.Apply(All, new TransactionFilter(AccountId: 3)).Should().Equal(Savings);
    }

    [Fact]
    public void Account_filter_keeps_only_the_accounts_own_half_of_a_split_transfer()
    {
        var outOfBank = Savings with { Kind = LedgerEntryKind.TransferOut };
        var intoSavings = Savings with
        {
            Kind = LedgerEntryKind.TransferIn, AccountId = 3, AccountName = "Savings",
            CounterpartAccountId = 2, CounterpartAccountName = "Bank"
        };

        LedgerFilter.Apply([outOfBank, intoSavings], new TransactionFilter(AccountId: 3)).Should().Equal(intoSavings);
    }

    [Fact]
    public void Amount_range_is_inclusive()
    {
        LedgerFilter.Apply(All, new TransactionFilter(MinAmount: 12.50m, MaxAmount: 500m)).Should().Equal(Savings, Lunch);
    }

    [Fact]
    public void IsActive_ignores_the_sort_order()
    {
        new TransactionFilter(Ascending: true).IsActive.Should().BeFalse();
        new TransactionFilter(SearchText: "  ").IsActive.Should().BeFalse();
        new TransactionFilter(AccountId: 1).IsActive.Should().BeTrue();
    }

    [Fact]
    public void Category_options_are_distinct_by_id_without_transfers()
    {
        var secondLunch = Lunch with { SourceId = 5 };

        LedgerFilter.CategoryOptions([Lunch, secondLunch, Salary, Savings]).Should().Equal(
            new FilterOption(10, "Food"), new FilterOption(20, "Salary"));
    }

    [Fact]
    public void Account_options_include_transfer_destinations()
    {
        LedgerFilter.AccountOptions(All).Should().Equal(
            new FilterOption(2, "Bank"), new FilterOption(1, "Cash"), new FilterOption(3, "Savings"));
    }
}
