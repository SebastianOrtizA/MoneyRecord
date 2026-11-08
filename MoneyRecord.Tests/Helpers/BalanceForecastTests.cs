using AwesomeAssertions;
using MoneyRecord.Helpers;
using Xunit;

namespace MoneyRecord.Tests.Helpers;

public sealed class BalanceForecastTests
{
    private static readonly DateTime Today = new(2026, 11, 1);

    private static ForecastEvent Event(int offset, long cents, int account = 1, bool estimate = false) =>
        new(Today.AddDays(offset), account, cents, ForecastSource.Recurring, estimate);

    private static Dictionary<int, long> Start(long cents) => new() { [1] = cents };

    [Fact]
    public void Has_a_point_per_day_from_today_through_the_horizon()
    {
        var series = BalanceForecast.Build(Start(1000), Today, 30, [])[1];

        series.Points.Should().HaveCount(31);
        series.Points[0].Should().Be(new ForecastPoint(Today, 1000));
        series.EndCents.Should().Be(1000);
        series.FirstNegativeDate.Should().BeNull();
    }

    [Fact]
    public void Events_apply_on_their_day_and_carry_forward()
    {
        var series = BalanceForecast.Build(Start(1000), Today, 10, [Event(2, -300), Event(2, 50), Event(5, 400)])[1];

        series.Points.Select(p => p.Cents).Should().Equal(1000, 1000, 750, 750, 750, 1150, 1150, 1150, 1150, 1150, 1150);
    }

    [Fact]
    public void An_overdue_event_counts_today_and_one_past_the_horizon_is_ignored()
    {
        var series = BalanceForecast.Build(Start(1000), Today, 5, [Event(-3, -200), Event(6, -999)])[1];

        series.Points[0].Cents.Should().Be(800);
        series.EndCents.Should().Be(800);
    }

    [Fact]
    public void Finds_the_lowest_point_and_the_first_negative_day()
    {
        var series = BalanceForecast.Build(Start(500), Today, 10, [Event(3, -700), Event(6, -100), Event(8, 1000)])[1];

        series.FirstNegativeDate.Should().Be(Today.AddDays(3));
        series.Lowest.Should().Be(new ForecastPoint(Today.AddDays(6), -300));
    }

    [Fact]
    public void The_lowest_point_is_the_earliest_of_equal_ones()
    {
        var series = BalanceForecast.Build(Start(500), Today, 6, [Event(2, -200), Event(4, 100), Event(5, -100)])[1];

        series.Lowest.Should().Be(new ForecastPoint(Today.AddDays(2), 300));
    }

    [Fact]
    public void Events_of_accounts_without_a_start_balance_are_ignored_and_accounts_stay_separate()
    {
        var series = BalanceForecast.Build(new Dictionary<int, long> { [1] = 100, [2] = 900 }, Today, 3,
            [Event(1, -50, 1), Event(1, -500, 2), Event(1, -7, 3)]);

        series[1].EndCents.Should().Be(50);
        series[2].EndCents.Should().Be(400);
        series.Should().NotContainKey(3);
    }

    [Fact]
    public void Combine_adds_the_series_day_by_day()
    {
        var series = BalanceForecast.Build(new Dictionary<int, long> { [1] = 100, [2] = 900 }, Today, 3,
            [Event(1, -150, 1), Event(2, 40, 2)]);

        var total = ForecastSeries.Combine([series[1], series[2]]);

        total.Points.Select(p => p.Cents).Should().Equal(1000, 850, 890, 890);
    }
}
