using AwesomeAssertions;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services;
using MoneyRecord.ViewModels;
using Xunit;

namespace MoneyRecord.Tests.Helpers;

/// <summary>The Reports hub's Core pieces: previous period, deltas, net worth, budget vs actual, drill-down, shared period.</summary>
public class ReportsHubTests
{
    private static DateTime MonthEnd(int year, int month) => new DateTime(year, month, 1).AddMonths(1).AddTicks(-1);

    [Fact]
    public void Previous_range_of_whole_months_is_the_same_number_of_months_before()
    {
        DateRangeHelper.GetPreviousRange(new DateTime(2026, 10, 1), MonthEnd(2026, 10))
            .Should().Be((new DateTime(2026, 9, 1), MonthEnd(2026, 9)));
        DateRangeHelper.GetPreviousRange(new DateTime(2026, 1, 1), MonthEnd(2026, 12))
            .Should().Be((new DateTime(2025, 1, 1), MonthEnd(2025, 12)));
        // March has 31 days, February 28: still the whole of February
        DateRangeHelper.GetPreviousRange(new DateTime(2026, 3, 1), MonthEnd(2026, 3))
            .Should().Be((new DateTime(2026, 2, 1), MonthEnd(2026, 2)));
    }

    [Fact]
    public void Previous_range_of_any_other_span_is_the_same_length_right_before()
    {
        var start = new DateTime(2026, 10, 6);
        var end = new DateTime(2026, 10, 13).AddTicks(-1);

        DateRangeHelper.GetPreviousRange(start, end).Should().Be((new DateTime(2026, 9, 29), start.AddTicks(-1)));
    }

    [Theory]
    [InlineData(112, 100, "▲")]
    [InlineData(80, 100, "▼")]
    public void Delta_says_the_direction_and_percentage_in_words(int current, int previous, string arrow)
    {
        var text = new PeriodDelta(current, previous).Text;

        text.Should().StartWith(arrow).And.Contain(Math.Abs(current - previous).ToString());
        text.Should().Be(string.Format(AppResources.DeltaVsPrevious, text[..text.IndexOf('%')] + "%"));
    }

    [Fact]
    public void Delta_handles_no_change_nothing_before_and_nothing_at_all()
    {
        new PeriodDelta(50, 50).Text.Should().Be(AppResources.DeltaNoChange);
        new PeriodDelta(50, 0).Text.Should().Be(AppResources.DeltaNothingBefore);
        new PeriodDelta(0, 0).Text.Should().BeEmpty();
        new PeriodDelta(6_000_300, 300).Text.Should().Contain(">").And.StartWith("▲");
        // A negative baseline (net cash flow): going from -100 to -50 is up
        new PeriodDelta(-50, -100).Change.Should().Be(0.5m);
    }

    [Fact]
    public void Net_worth_works_back_from_todays_total_counting_only_the_given_accounts()
    {
        // Today: 1000 in accounts 1 + 2. September: +200 income (1). October: -50 expense (2), transfer 1 -> 3 of 100
        // (3 isn't counted, so it leaves), a transfer 1 -> 2 of 30 (both counted: no change), an entry on account 9.
        var points = NetWorthBuilder.Build(1000m, [1, 2],
        [
            new Transaction { AccountId = 1, Type = TransactionType.Income, Amount = 200m, Date = new DateTime(2026, 9, 10) },
            new Transaction { AccountId = 2, Type = TransactionType.Expense, Amount = 50m, Date = new DateTime(2026, 10, 2) },
            new Transaction { AccountId = 9, Type = TransactionType.Income, Amount = 999m, Date = new DateTime(2026, 10, 3) }
        ],
        [
            new Transfer { SourceAccountId = 1, DestinationAccountId = 3, Amount = 100m, DestinationAmount = 100m, Date = new DateTime(2026, 10, 5) },
            new Transfer { SourceAccountId = 1, DestinationAccountId = 2, Amount = 30m, DestinationAmount = 30m, Date = new DateTime(2026, 10, 6) }
        ], new DateTime(2026, 8, 1), 3);

        points.Select(p => (p.Month.Month, p.Value)).Should().Equal((8, 950m), (9, 1150m), (10, 1000m));
    }

    [Fact]
    public void Budget_vs_actual_takes_each_budget_for_a_whole_calendar_month_and_only_budgeted_categories()
    {
        var rows = BudgetVsActualBuilder.Build(
        [
            new Budget { CategoryId = 1, LimitAmount = 300m, Period = BudgetPeriod.Month, CreatedDate = new DateTime(2026, 1, 1) },
            new Budget { CategoryId = 2, LimitAmount = 10m, Period = BudgetPeriod.Day, CreatedDate = new DateTime(2026, 1, 1) },
            new Budget { CategoryId = 3, LimitAmount = 1200m, Period = BudgetPeriod.Year, CreatedDate = new DateTime(2026, 1, 1) }
        ],
        [
            new Transaction { CategoryId = 1, Type = TransactionType.Expense, Amount = 40m, Date = new DateTime(2026, 2, 3) },
            new Transaction { CategoryId = 2, Type = TransactionType.Expense, Amount = -5m, Date = new DateTime(2026, 2, 4) },
            new Transaction { CategoryId = 7, Type = TransactionType.Expense, Amount = 999m, Date = new DateTime(2026, 2, 5) },
            new Transaction { CategoryId = 1, Type = TransactionType.Income, Amount = 999m, Date = new DateTime(2026, 2, 6) }
        ], new DateTime(2026, 2, 1), 2);

        // February 2026: 300 + 10 × 28 + 1200 / 12; March: 300 + 10 × 31 + 100
        rows.Should().Equal(
            new MonthBudget(new DateTime(2026, 2, 1), 680m, 45m),
            new MonthBudget(new DateTime(2026, 3, 1), 710m, 0m));
    }

    [Fact]
    public void A_budget_counts_from_the_month_it_was_created()
    {
        var rows = BudgetVsActualBuilder.Build(
            [new Budget { CategoryId = 1, LimitAmount = 100m, Period = BudgetPeriod.Month, CreatedDate = new DateTime(2026, 3, 20) }],
            [
                new Transaction { CategoryId = 1, Type = TransactionType.Expense, Amount = 70m, Date = new DateTime(2026, 2, 3) },
                new Transaction { CategoryId = 1, Type = TransactionType.Expense, Amount = 30m, Date = new DateTime(2026, 3, 3) }
            ], new DateTime(2026, 2, 1), 2);

        rows.Select(r => (r.Limit, r.Spent)).Should().Equal((0m, 0m), (100m, 30m));
    }

    [Fact]
    public void Drill_down_is_taken_once()
    {
        var drillDown = new TransactionsDrillDown();
        drillDown.TryTake(out _, out _).Should().BeFalse();

        drillDown.Request(5, TransactionType.Expense);

        drillDown.TryTake(out var category, out var type).Should().BeTrue();
        (category, type).Should().Be((5, TransactionType.Expense));
        drillDown.TryTake(out _, out _).Should().BeFalse();
    }

    [Fact]
    public void Shared_period_shows_a_month_with_one_change()
    {
        var period = new PeriodViewModel();
        var changes = 0;
        period.Changed += () => changes++;

        period.ShowMonth(new DateTime(2026, 2, 17));

        changes.Should().Be(1);
        period.IsCustomPeriodSelected.Should().BeTrue();
        period.Range.Should().Be((new DateTime(2026, 2, 1), MonthEnd(2026, 2)));
        period.PreviousRange.Should().Be((new DateTime(2026, 1, 1), MonthEnd(2026, 1)));
    }

    [Fact]
    public void Shared_period_tells_month_count_changes_apart_from_period_changes()
    {
        var period = new PeriodViewModel();
        var (changes, monthChanges) = (0, 0);
        period.Changed += () => changes++;
        period.MonthCountChanged += () => monthChanges++;

        period.MonthCount = 12;
        period.SelectedPeriod = period.Periods.First(p => p.Type == PeriodType.CalendarYear);

        (changes, monthChanges).Should().Be((1, 1));
        period.SelectedPeriod.Type.Should().Be(PeriodType.CalendarYear);
    }

    [Fact]
    public void Whole_months_is_one_rule_for_dates_from_a_first_to_a_last_day()
    {
        DateRangeHelper.WholeMonths(new DateTime(2026, 10, 1), new DateTime(2026, 10, 31)).Should().Be(1);
        DateRangeHelper.WholeMonths(new DateTime(2026, 1, 1), MonthEnd(2026, 12)).Should().Be(12);
        DateRangeHelper.WholeMonths(new DateTime(2026, 10, 2), new DateTime(2026, 10, 31)).Should().BeNull();
        DateRangeHelper.WholeMonths(new DateTime(2026, 10, 1), new DateTime(2026, 10, 30)).Should().BeNull();
        DateRangeHelper.WholeMonths(default, default).Should().BeNull();
    }

    [Fact]
    public void A_watch_follows_the_period_until_it_is_disposed()
    {
        var period = new PeriodViewModel();
        var (changes, months) = (0, 0);
        var watch = period.WatchChanges(() => changes++);
        var monthsWatch = period.WatchMonthCount(() => months++);

        period.ShowMonth(new DateTime(2026, 5, 1));
        period.MonthCount = 3;
        watch.Dispose();
        monthsWatch.Dispose();
        watch.Dispose();
        period.ShowMonth(new DateTime(2026, 6, 1));
        period.MonthCount = 12;

        (changes, months).Should().Be((1, 1));
    }
}
