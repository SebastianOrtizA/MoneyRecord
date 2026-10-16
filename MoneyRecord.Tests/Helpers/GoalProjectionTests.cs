using AwesomeAssertions;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using Xunit;

namespace MoneyRecord.Tests.Helpers;

public class GoalProjectionTests
{
    private static readonly DateTime Today = new(2026, 10, 16);

    [Fact]
    public void Pace_window_is_three_months_or_since_the_first_account()
    {
        GoalProjection.PaceStart(Today, new DateTime(2020, 1, 1)).Should().Be(new DateTime(2026, 7, 16));
        GoalProjection.PaceStart(Today, new DateTime(2026, 9, 1, 14, 30, 0)).Should().Be(new DateTime(2026, 9, 1));
        GoalProjection.PaceStart(Today, null).Should().Be(new DateTime(2026, 7, 16));
    }

    [Fact]
    public void Monthly_pace_is_the_change_per_month_and_needs_four_weeks()
    {
        var start = Today.AddMonths(-3);
        GoalProjection.MonthlyPace(900m, start, Today).Should().BeApproximately(300m, 3m);
        GoalProjection.MonthlyPace(-300m, start, Today).Should().BeNegative();
        GoalProjection.MonthlyPace(500m, Today.AddDays(-27), Today).Should().BeNull("27 days say little about a month");
        GoalProjection.MonthlyPace(500m, Today.AddDays(-28), Today).Should().NotBeNull();
    }

    [Fact]
    public void Needed_per_month_spreads_the_rest_over_the_months_left()
    {
        GoalProjection.NeededPerMonth(1200m, Today.AddYears(1), Today).Should().BeApproximately(100m, 0.5m);
        GoalProjection.NeededPerMonth(1200m, Today.AddDays(10), Today).Should().Be(1200m, "in the last month all of it");
        GoalProjection.NeededPerMonth(1200m, Today, Today).Should().Be(1200m);
    }

    [Fact]
    public void Projected_date_follows_the_pace()
    {
        GoalProjection.ProjectedDate(600m, 200m, Today).Should().BeCloseTo(Today.AddMonths(3), TimeSpan.FromDays(2));
        GoalProjection.ProjectedDate(600m, 0m, Today).Should().BeNull();
        GoalProjection.ProjectedDate(600m, -50m, Today).Should().BeNull();
        GoalProjection.ProjectedDate(600m, null, Today).Should().BeNull();
        GoalProjection.ProjectedDate(1_000_000_000m, 0.01m, Today).Should().BeNull("further than a century is not in sight");
    }

    [Theory]
    [InlineData(0, null, null, null, GoalStatus.Reached)]
    [InlineData(100, null, 50, 2027, GoalStatus.None)] // no target date
    [InlineData(100, 2027, null, null, GoalStatus.None)] // too little history
    [InlineData(100, 2027, 50, 2026, GoalStatus.OnTrack)]
    [InlineData(100, 2027, 50, 2028, GoalStatus.Behind)]
    [InlineData(100, 2027, 0, null, GoalStatus.Behind)] // not saving
    [InlineData(100, 2025, 50, 2026, GoalStatus.Behind)] // the date has passed
    public void Status(int remaining, int? targetYear, int? pace, int? projectedYear, GoalStatus expected)
    {
        DateTime? Year(int? year) => year is { } y ? new DateTime(y, 11, 1) : null;

        GoalProjection.Status(remaining, Year(targetYear), pace, Year(projectedYear), Today).Should().Be(expected);
    }

    [Fact]
    public void Progress_reached_has_no_plan_and_full_bar()
    {
        var goal = new SavingsGoal { Name = "Trip", TargetAmount = 1000m, CurrencyCode = "USD", TargetDate = Today.AddMonths(6) };

        var progress = new GoalProgress(goal, [new Account { Id = 1, Name = "Savings" }], [], 1200m, 100m, Today);

        progress.Status.Should().Be(GoalStatus.Reached);
        progress.ProgressPercentage.Should().Be(100);
        progress.Remaining.Should().Be(0);
        progress.NeededPerMonth.Should().BeNull();
        progress.HasPlan.Should().BeFalse();
        progress.HasPace.Should().BeFalse();
    }

    [Fact]
    public void Progress_open_goal_has_the_needed_amount_and_a_projection()
    {
        var goal = new SavingsGoal { Name = "Trip", TargetAmount = 1000m, CurrencyCode = "USD", TargetDate = Today.AddMonths(6) };

        var progress = new GoalProgress(goal, [new Account { Id = 1, Name = "Savings" }, new Account { Id = 2, Name = "Box" }],
            [new Account { Id = 3, Name = "Euros" }], 400m, 50m, Today);

        progress.ProgressPercentage.Should().Be(40);
        progress.NeededPerMonth.Should().BeApproximately(100m, 1m);
        progress.ProjectedDate.Should().BeCloseTo(Today.AddMonths(12), TimeSpan.FromDays(3));
        progress.Status.Should().Be(GoalStatus.Behind);
        progress.Level.Should().Be(BudgetLevel.Warning);
        progress.AccountNames.Should().Be("Savings · Box");
        progress.AccountIds.Should().Equal(1, 2, 3);
        progress.HasUncounted.Should().BeTrue();
        progress.HasPlan.Should().BeTrue();
        progress.HasPace.Should().BeTrue();
    }
}
