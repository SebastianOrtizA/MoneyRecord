using AwesomeAssertions;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using Xunit;

namespace MoneyRecord.Tests.Helpers;

[Collection("Language")]
public class YearReviewBuilderTests
{
    private static readonly DateTime EndOfYear = new(2027, 1, 10);

    private static Transaction Entry(int month, int day, decimal amount, TransactionType type = TransactionType.Expense,
        int categoryId = 1, string category = "Food", string? tags = null, int? payeeId = null, string payee = "", string description = "") =>
        new()
        {
            Type = type, Amount = amount, Date = new DateTime(2026, month, day), CategoryId = categoryId, CategoryName = category,
            Tags = tags, PayeeId = payeeId, PayeeName = payee, Description = description
        };

    private static YearReview Build(IEnumerable<Transaction> entries, DateTime? today = null) =>
        YearReviewBuilder.Build(2026, entries, [1, 2, 3, 4], "EUR", false, "(none)", today ?? EndOfYear);

    [Fact]
    public void Totals_savings_rate_and_other_years_are_left_out()
    {
        var review = Build([Entry(1, 5, 300, TransactionType.Income), Entry(2, 5, 75), new Transaction
        {
            Type = TransactionType.Expense, Amount = 999, Date = new DateTime(2025, 12, 31)
        }]);

        review.Income.Should().Be(300m);
        review.Expense.Should().Be(75m);
        review.Net.Should().Be(225m);
        review.SavingsRate.Should().Be(75.0);
    }

    [Fact]
    public void No_income_means_no_savings_rate_and_no_entries_means_no_data()
    {
        Build([Entry(3, 1, 10)]).SavingsRate.Should().BeNull();
        Build([]).HasData.Should().BeFalse();
    }

    [Fact]
    public void Highest_and_lowest_spending_months_need_two_months()
    {
        var one = Build([Entry(3, 1, 10), Entry(3, 2, 20)], new DateTime(2026, 3, 31));
        one.HighestMonth.Should().BeNull();
        one.LowestMonth.Should().BeNull();

        var two = Build([Entry(3, 1, 10), Entry(5, 2, 50), Entry(5, 3, 5), Entry(8, 1, 25)], new DateTime(2026, 8, 31));
        two.HighestMonth.Should().Be(new MonthSpending(5, 55m));
        two.LowestMonth.Should().Be(new MonthSpending(4, 0m));
    }

    [Fact]
    public void A_month_without_spending_counts_as_the_lowest()
    {
        // March has no expense: it is the lowest, not February
        var review = Build([Entry(1, 5, 30), Entry(2, 5, 10), Entry(4, 5, 20)], new DateTime(2026, 4, 30));

        review.LowestMonth.Should().Be(new MonthSpending(3, 0m));
        review.HighestMonth.Should().Be(new MonthSpending(1, 30m));
    }

    [Fact]
    public void Top_categories_payees_and_the_biggest_expense()
    {
        var review = Build([
            Entry(1, 1, 100, categoryId: 1, category: "Food", payeeId: 7, payee: "Mart", description: "Weekly shop"),
            Entry(2, 1, 40, categoryId: 2, category: "Fun", payeeId: 8, payee: "Cinema"),
            Entry(2, 2, 10, categoryId: 3, category: "Gifts"),
            Entry(2, 3, 5, categoryId: 4, category: "Misc")]);

        review.TopCategories.Select(c => c.CategoryName).Should().Equal("Food", "Fun", "Gifts");
        review.TopPayees.Select(p => p.Name).Should().Equal("Mart", "Cinema");
        review.Biggest.Should().Be(new BiggestExpense("Weekly shop", "Food", new DateTime(2026, 1, 1), 100m));
    }

    [Fact]
    public void Most_used_tag_counts_expenses_ignoring_case()
    {
        var review = Build([Entry(1, 1, 5, tags: "Trip"), Entry(1, 2, 5, tags: "trip, gift"), Entry(1, 3, 5, tags: "gift"),
            Entry(1, 4, 5, tags: "Trip")]);

        review.TopTag.Should().Be(new TagUse("Trip", 3));
    }

    [Fact]
    public void No_spend_days_count_from_the_first_entry_to_year_end_or_today()
    {
        // Jan 1..Jan 10 of 2026: spending on 2 of those 10 days
        var running = Build([Entry(1, 1, 5), Entry(1, 4, 5), Entry(1, 4, 5)], new DateTime(2026, 1, 10));
        running.DaysCounted.Should().Be(10);
        running.NoSpendDays.Should().Be(8);

        // A finished year counts to Dec 31
        var done = Build([Entry(12, 30, 5)]);
        done.DaysCounted.Should().Be(2);
        done.NoSpendDays.Should().Be(1);
    }
}
