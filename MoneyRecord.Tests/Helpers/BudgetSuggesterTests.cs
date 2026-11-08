using AwesomeAssertions;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using Xunit;

namespace MoneyRecord.Tests.Helpers;

public class BudgetSuggesterTests
{
    private static IReadOnlyDictionary<int, BudgetSpent> Month(params (int Category, decimal Spent)[] rows) =>
        rows.ToDictionary(r => r.Category, r => new BudgetSpent(r.Spent, []));

    [Theory]
    [InlineData(0.3, 0)]
    [InlineData(12.4, 12)]
    [InlineData(99.6, 100)]
    [InlineData(104, 100)]
    [InlineData(255, 260)]
    public void Limits_are_round_numbers(double average, double expected) =>
        BudgetSuggester.Round((decimal)average).Should().Be((decimal)expected);

    [Fact]
    public void A_category_gets_the_average_of_three_months_and_a_month_without_spending_counts_as_zero()
    {
        var food = new Category { Id = 1, Name = "Food", IconCode = "F0001" };

        var suggestions = BudgetSuggester.Suggest([food], [Month((1, 300)), Month((1, 600)), Month()]);

        suggestions.Should().ContainSingle().Which.Should().Be(new BudgetSuggestion(1, "Food", "F0001", 300m));
    }

    [Fact]
    public void Categories_without_spending_are_left_out_and_the_biggest_comes_first()
    {
        var categories = new[]
        {
            new Category { Id = 1, Name = "Fun" }, new Category { Id = 2, Name = "Rent" }, new Category { Id = 3, Name = "Unused" }
        };

        var suggestions = BudgetSuggester.Suggest(categories, [Month((1, 30), (2, 900)), Month((1, 60), (2, 900)), Month((1, 30), (2, 900))]);

        suggestions.Select(s => (s.Name, s.Amount)).Should().Equal(("Rent", 900m), ("Fun", 40m));
    }

    [Fact]
    public void The_overall_budget_spends_every_category_and_sums_what_could_not_be_counted_per_currency()
    {
        var usd = new CurrencyAmount("USD", 5m);
        var byCategory = new Dictionary<int, BudgetSpent>
        {
            [1] = new(20m, [usd]),
            [2] = new(30m, [new CurrencyAmount("USD", 7m)])
        };

        var spent = BudgetSpent.For(byCategory, Budget.AllSpending);

        spent.Counted.Should().Be(50m);
        spent.Uncounted.Should().ContainSingle().Which.Should().Be(new CurrencyAmount("USD", 12m));
        BudgetSpent.For(byCategory, 2).Counted.Should().Be(30m);
        BudgetSpent.For(byCategory, 9).Should().Be(BudgetSpent.None);
    }

    [Fact]
    public void Budget_vs_actual_uses_the_overall_budget_alone_when_there_is_one()
    {
        var rows = BudgetVsActualBuilder.Build(
        [
            new Budget { CategoryId = Budget.AllSpending, LimitAmount = 1000m, Period = BudgetPeriod.Month, CreatedDate = new DateTime(2026, 1, 1) },
            new Budget { CategoryId = 1, LimitAmount = 300m, Period = BudgetPeriod.Month, CreatedDate = new DateTime(2026, 1, 1) }
        ],
        [
            new Transaction { CategoryId = 1, Type = TransactionType.Expense, Amount = 40m, Date = new DateTime(2026, 2, 3) },
            new Transaction { CategoryId = 7, Type = TransactionType.Expense, Amount = 60m, Date = new DateTime(2026, 2, 5) },
            new Transaction { CategoryId = 7, Type = TransactionType.Expense, Amount = 5m, Date = new DateTime(2026, 3, 5) }
        ], new DateTime(2026, 2, 1), 2);

        rows.Should().Equal(new MonthBudget(new DateTime(2026, 2, 1), 1000m, 100m), new MonthBudget(new DateTime(2026, 3, 1), 1000m, 5m));
    }
}
