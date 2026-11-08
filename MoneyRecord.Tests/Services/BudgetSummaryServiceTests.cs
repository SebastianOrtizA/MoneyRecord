using AwesomeAssertions;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;
using NSubstitute;
using Xunit;

namespace MoneyRecord.Tests.Services;

public class BudgetSummaryServiceTests
{
    private readonly IBudgetRepository _budgets = Substitute.For<IBudgetRepository>();
    private readonly ICategoryRepository _categories = Substitute.For<ICategoryRepository>();
    private readonly IConversionService _conversion = Substitute.For<IConversionService>();
    private readonly BudgetSummaryService _sut;

    public BudgetSummaryServiceTests()
    {
        _sut = new BudgetSummaryService(_budgets, _categories, _conversion);
        _conversion.GetExpensesByCategoryAsync(default, default).ReturnsForAnyArgs(new Dictionary<int, BudgetSpent>());
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
        _conversion.GetExpensesByCategoryAsync(start, end).Returns(new Dictionary<int, BudgetSpent> { [1] = new(20m, []), [2] = new(120m, []) });

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
    public async Task Only_the_counted_spending_counts_and_the_rest_is_listed()
    {
        _budgets.GetActiveBudgetsAsync().Returns(new List<Budget>
        {
            new() { Id = 10, CategoryId = 1, LimitAmount = 100m, Period = BudgetPeriod.Month }
        });
        _conversion.GetExpensesByCategoryAsync(default, default).ReturnsForAnyArgs(new Dictionary<int, BudgetSpent>
        {
            [1] = new(30m, [new("ZZZ", 500m)])
        });

        var progress = (await _sut.GetSummaryAsync(PeriodType.CalendarMonth)).Single();

        progress.SpentAmount.Should().Be(30m);
        progress.IsOverBudget.Should().BeFalse();
        progress.HasUncounted.Should().BeTrue();
        progress.UncountedText.Should().Contain(MoneyFormatter.Format(500m, "ZZZ"));
    }

    [Fact]
    public async Task Rollover_adds_last_months_unspent_money_to_the_limit_only_in_the_budgets_own_period()
    {
        var now = DateTime.Now;
        var lastMonth = new DateTime(now.Year, now.Month, 1).AddMonths(-1);
        _budgets.GetActiveBudgetsAsync().Returns(new List<Budget>
        {
            new() { Id = 10, CategoryId = 1, LimitAmount = 100m, Period = BudgetPeriod.Month, RolloverUnspent = true, CreatedDate = lastMonth.AddDays(2) }
        });
        _conversion.GetExpensesByCategoryAsync(lastMonth, lastMonth.AddMonths(1).AddTicks(-1))
            .Returns(new Dictionary<int, BudgetSpent> { [1] = new(60m, []) });

        var month = (await _sut.GetSummaryAsync(PeriodType.CalendarMonth)).Single();
        var year = (await _sut.GetSummaryAsync(PeriodType.CalendarYear)).Single();

        month.CarriedOverAmount.Should().Be(40m);
        month.LimitAmount.Should().Be(140m);
        month.OriginalLimitAmount.Should().Be(100m);
        month.RolloverEnabled.Should().BeTrue();
        month.CarriedOverText.Should().Contain(MoneyFormatter.Format(40m));
        year.CarriedOverAmount.Should().Be(0m);
        year.LimitAmount.Should().Be(1200m);
    }

    [Fact]
    public async Task A_budget_without_rollover_keeps_its_limit()
    {
        _budgets.GetActiveBudgetsAsync().Returns(new List<Budget>
        {
            new() { Id = 10, CategoryId = 1, LimitAmount = 100m, Period = BudgetPeriod.Month, CreatedDate = new DateTime(2020, 1, 1) }
        });

        var progress = (await _sut.GetSummaryAsync(PeriodType.CalendarMonth)).Single();

        progress.LimitAmount.Should().Be(100m);
        progress.HasCarriedOver.Should().BeFalse();
        progress.CarriedOverText.Should().BeEmpty();
    }

    [Fact]
    public async Task Last_month_has_no_daily_allowance_because_it_is_over()
    {
        _budgets.GetActiveBudgetsAsync().Returns(new List<Budget>
        {
            new() { Id = 10, CategoryId = 1, LimitAmount = 100m, Period = BudgetPeriod.Month }
        });

        (await _sut.GetSummaryAsync(PeriodType.LastMonth)).Single().HasDailyAllowance.Should().BeFalse();
    }

    [Fact]
    public async Task The_overall_budget_counts_every_category_under_All_spending()
    {
        _budgets.GetActiveBudgetsAsync().Returns(new List<Budget>
        {
            new() { Id = 10, CategoryId = Budget.AllSpending, LimitAmount = 200m, Period = BudgetPeriod.Month }
        });
        var (start, end) = DateRangeHelper.GetDateRange(PeriodType.CalendarMonth);
        _conversion.GetExpensesByCategoryAsync(start, end).Returns(new Dictionary<int, BudgetSpent> { [1] = new(20m, []), [2] = new(120m, []) });

        var progress = (await _sut.GetSummaryAsync(PeriodType.CalendarMonth)).Single();

        progress.CategoryName.Should().Be(AppResources.AllSpending);
        progress.CategoryId.Should().Be(Budget.AllSpending);
        progress.SpentAmount.Should().Be(140m);
        progress.LimitAmount.Should().Be(200m);
    }

    [Fact]
    public async Task Suggestions_average_the_last_three_full_months_of_categories_without_a_budget()
    {
        _budgets.GetActiveBudgetsAsync().Returns(new List<Budget> { new() { Id = 10, CategoryId = 2, LimitAmount = 50m } });
        _categories.GetByTypeAsync(CategoryType.Expense).Returns(new List<Category>
        {
            new() { Id = 1, Name = "Food", IconCode = "F0001" },
            new() { Id = 2, Name = "Fun", IconCode = "F0002" }
        });
        var today = new DateTime(2026, 11, 7);
        foreach (var month in new[] { 8, 9, 10 })
        {
            _conversion.GetExpensesByCategoryAsync(new DateTime(2026, month, 1), new DateTime(2026, month, 1).AddMonths(1).AddTicks(-1))
                .Returns(new Dictionary<int, BudgetSpent> { [1] = new(100m * (month - 7), []), [2] = new(999m, []) });
        }

        var suggestions = await _sut.GetSuggestionsAsync(today);

        // (100 + 200 + 300) / 3; Fun already has a budget
        suggestions.Should().ContainSingle().Which.Should().Be(new BudgetSuggestion(1, "Food", "F0001", 200m));
    }
}
