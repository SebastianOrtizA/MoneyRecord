using AwesomeAssertions;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using Xunit;

namespace MoneyRecord.Tests.Helpers;

public class CategoryTrendBuilderTests
{
    private static Transaction Expense(int category, decimal amount, int month, int day = 5) =>
        new() { Type = TransactionType.Expense, CategoryId = category, Amount = amount, Date = new DateTime(2026, month, day) };

    [Fact]
    public void Sums_the_category_per_month_and_leaves_empty_months_at_zero()
    {
        var trend = CategoryTrendBuilder.Build(
            [Expense(1, 10, 1), Expense(1, 5, 1, 20), Expense(2, 99, 1), Expense(1, 30, 3)],
            TransactionType.Expense, 1, new DateTime(2026, 1, 15), 3);

        trend.Months.Select(m => m.Amount).Should().Equal(15m, 0m, 30m);
        trend.Months[0].Month.Should().Be(new DateTime(2026, 1, 1));
    }

    [Fact]
    public void Income_and_expense_of_the_same_category_do_not_mix()
    {
        var income = new Transaction { Type = TransactionType.Income, CategoryId = 1, Amount = 500, Date = new DateTime(2026, 1, 2) };

        CategoryTrendBuilder.Build([income, Expense(1, 10, 1)], TransactionType.Expense, 1, new DateTime(2026, 1, 1), 1)
            .Total.Should().Be(10m);
    }

    [Fact]
    public void Average_highest_and_lowest_leave_out_the_month_still_going_on()
    {
        // Jan 10, Feb 30, Mar (so far) 100
        var trend = CategoryTrendBuilder.Build([Expense(1, 10, 1), Expense(1, 30, 2), Expense(1, 100, 3)],
            TransactionType.Expense, 1, new DateTime(2026, 1, 1), 3);

        trend.Average.Should().Be(20m);
        trend.Highest.Amount.Should().Be(30m);
        trend.Lowest.Amount.Should().Be(10m);
        trend.Current.Amount.Should().Be(100m);
        trend.CurrentVsAverage.Change.Should().Be(4m);
    }

    [Fact]
    public void A_single_month_is_its_own_average()
    {
        var trend = CategoryTrendBuilder.Build([Expense(1, 40, 3)], TransactionType.Expense, 1, new DateTime(2026, 3, 1), 1);

        trend.Average.Should().Be(40m);
        trend.Highest.Should().BeSameAs(trend.Lowest);
    }

    [Fact]
    public void Nothing_in_the_category_has_no_data()
    {
        CategoryTrendBuilder.Build([Expense(2, 40, 3)], TransactionType.Expense, 1, new DateTime(2026, 1, 1), 3)
            .HasData.Should().BeFalse();
    }
}
