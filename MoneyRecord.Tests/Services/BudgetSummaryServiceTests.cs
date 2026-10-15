using AwesomeAssertions;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;
using NSubstitute;
using Xunit;

namespace MoneyRecord.Tests.Services;

public class BudgetSummaryServiceTests
{
    private readonly IBudgetRepository _budgets = Substitute.For<IBudgetRepository>();
    private readonly ICategoryRepository _categories = Substitute.For<ICategoryRepository>();
    private readonly IBalanceService _balance = Substitute.For<IBalanceService>();
    private readonly BudgetSummaryService _sut;
    private static string Base => MoneyFormatter.ResolveCurrencyCode(null);

    public BudgetSummaryServiceTests()
    {
        _sut = new BudgetSummaryService(_budgets, _categories, _balance);
        _balance.GetExpensesByCategoryAsync(default, default).ReturnsForAnyArgs(new Dictionary<int, List<CurrencyAmount>>());
        _categories.GetAllAsync().Returns(new List<Category>
        {
            new() { Id = 1, Name = "Food", IconCode = "F0001" },
            new() { Id = 2, Name = "Fun", IconCode = "F0002" }
        });
    }

    [Fact]
    public async Task No_budgets_is_an_empty_summary()
    {
        _budgets.GetActiveBudgetsAsync().Returns(new List<Budget>());

        (await _sut.GetSummaryAsync(PeriodType.CalendarMonth)).Should().BeEmpty();
    }

    [Fact]
    public async Task Spending_is_read_for_the_selected_period_and_sorted_by_progress()
    {
        _budgets.GetActiveBudgetsAsync().Returns(new List<Budget>
        {
            new() { Id = 10, CategoryId = 1, LimitAmount = 100m, Period = BudgetPeriod.Month },
            new() { Id = 11, CategoryId = 2, LimitAmount = 100m, Period = BudgetPeriod.Month }
        });
        var (start, end) = DateRangeHelper.GetDateRange(PeriodType.CalendarMonth);
        _balance.GetExpensesByCategoryAsync(start, end).Returns(new Dictionary<int, List<CurrencyAmount>> { [1] = [new(Base, 20m)], [2] = [new(Base, 120m)] });

        var summary = await _sut.GetSummaryAsync(PeriodType.CalendarMonth);

        summary.Select(b => b.CategoryName).Should().Equal("Fun", "Food");
        summary[0].IsOverBudget.Should().BeTrue();
        summary[1].SpentAmount.Should().Be(20m);
        summary[1].CategoryIconCode.Should().Be("F0001");
    }

    [Fact]
    public async Task The_limit_is_projected_onto_the_period_like_the_Budget_page()
    {
        // A daily budget over a calendar month: the Home card used to compare it with today's spending only
        _budgets.GetActiveBudgetsAsync().Returns(new List<Budget>
        {
            new() { Id = 10, CategoryId = 1, LimitAmount = 10m, Period = BudgetPeriod.Day }
        });

        var progress = (await _sut.GetSummaryAsync(PeriodType.CalendarMonth)).Single();

        progress.OriginalLimitAmount.Should().Be(10m);
        progress.LimitAmount.Should().Be(BudgetProjectionHelper.CalculateProjectedLimit(10m, BudgetPeriod.Day, PeriodType.CalendarMonth));
        progress.LimitAmount.Should().BeGreaterThan(10m);
    }

    [Fact]
    public async Task A_budget_whose_category_is_gone_is_skipped()
    {
        _budgets.GetActiveBudgetsAsync().Returns(new List<Budget>
        {
            new() { Id = 10, CategoryId = 99, LimitAmount = 10m, Period = BudgetPeriod.Month }
        });

        (await _sut.GetSummaryAsync(PeriodType.CalendarMonth)).Should().BeEmpty();
    }

    [Fact]
    public async Task Only_base_currency_expenses_count_and_the_others_are_listed()
    {
        _budgets.GetActiveBudgetsAsync().Returns(new List<Budget>
        {
            new() { Id = 10, CategoryId = 1, LimitAmount = 100m, Period = BudgetPeriod.Month }
        });
        _balance.GetExpensesByCategoryAsync(default, default).ReturnsForAnyArgs(new Dictionary<int, List<CurrencyAmount>>
        {
            [1] = [new(Base, 30m), new("ZZZ", 500m)]
        });

        var progress = (await _sut.GetSummaryAsync(PeriodType.CalendarMonth)).Single();

        progress.SpentAmount.Should().Be(30m);
        progress.IsOverBudget.Should().BeFalse();
        progress.HasUncounted.Should().BeTrue();
        progress.UncountedText.Should().Contain(MoneyFormatter.Format(500m, "ZZZ"));
    }
}
