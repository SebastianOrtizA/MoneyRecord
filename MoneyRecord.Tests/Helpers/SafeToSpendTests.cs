using AwesomeAssertions;
using MoneyRecord.Helpers;
using MoneyRecord.Services;
using Xunit;

namespace MoneyRecord.Tests.Helpers;

public class SafeToSpendTests
{
    private static readonly DateTime Today = new(2026, 11, 10);

    private static ForecastEvent Out(int day, long cents, ForecastSource source = ForecastSource.Recurring, bool estimate = false) =>
        new(new DateTime(2026, 11, day), 1, -cents, source, estimate);

    private static ForecastEvent In(int day, long cents) => new(new DateTime(2026, 11, day), 1, cents, ForecastSource.Recurring, false);

    [Fact]
    public void Until_the_next_income_the_income_day_is_covered_by_the_income()
    {
        var result = SafeToSpend.Compute(100_000, [Out(12, 20_000), Out(14, 5_000), In(14, 300_000), Out(20, 90_000)], Today, SafeToSpendHorizon.NextIncome);

        result.Until.Should().Be(new DateTime(2026, 11, 14));
        result.Days.Should().Be(4);
        // 14th's own outflow is after the covered days (10 to 13)
        result.TotalCents.Should().Be(80_000);
        result.PerDayCents.Should().Be(20_000);
    }

    [Fact]
    public void No_income_rule_falls_back_to_the_end_of_the_month()
    {
        var result = SafeToSpend.Compute(100_000, [Out(15, 10_000), Out(30, 10_000)], Today, SafeToSpendHorizon.NextIncome);

        result.Until.Should().Be(new DateTime(2026, 11, 30));
        result.Days.Should().Be(21);
        result.TotalCents.Should().Be(80_000);
    }

    [Fact]
    public void End_of_month_ignores_the_income_and_never_counts_it()
    {
        var result = SafeToSpend.Compute(50_000, [In(12, 400_000), Out(20, 20_000)], Today, SafeToSpendHorizon.EndOfMonth);

        result.Until.Should().Be(new DateTime(2026, 11, 30));
        result.TotalCents.Should().Be(30_000);
    }

    [Fact]
    public void Outflows_after_the_horizon_are_left_out()
    {
        SafeToSpend.Compute(10_000, [new ForecastEvent(new DateTime(2026, 12, 2), 1, -1_000, ForecastSource.Recurring, false)], new DateTime(2026, 11, 30), SafeToSpendHorizon.EndOfMonth)
            .TotalCents.Should().Be(10_000);
        SafeToSpend.Compute(10_000, [Out(2, 1_000)], new DateTime(2026, 11, 1), SafeToSpendHorizon.EndOfMonth)
            .TotalCents.Should().Be(9_000);
    }

    [Fact]
    public void Last_day_of_the_month_is_one_day()
    {
        var result = SafeToSpend.Compute(10_000, [], new DateTime(2026, 11, 30), SafeToSpendHorizon.EndOfMonth);

        result.Days.Should().Be(1);
        result.PerDayCents.Should().Be(10_000);
    }

    [Fact]
    public void Income_tomorrow_covers_today_only()
    {
        var result = SafeToSpend.Compute(10_000, [In(11, 500_000), Out(11, 2_000)], Today, SafeToSpendHorizon.NextIncome);

        result.Days.Should().Be(1);
        result.TotalCents.Should().Be(10_000);
    }

    [Fact]
    public void Overdue_and_estimated_outflows_count_and_a_shortfall_is_negative()
    {
        var overdue = new ForecastEvent(new DateTime(2026, 11, 3), 1, -60_000, ForecastSource.Recurring, true);
        var result = SafeToSpend.Compute(50_000, [overdue, Out(11, 30_000, ForecastSource.Debt)], Today, SafeToSpendHorizon.EndOfMonth);

        result.TotalCents.Should().Be(-40_000);
        result.IsNegative.Should().BeTrue();
    }

    [Fact]
    public void Transfers_in_are_not_an_income()
    {
        var transferIn = new ForecastEvent(new DateTime(2026, 11, 12), 1, 90_000, ForecastSource.Scheduled, false);

        SafeToSpend.NextIncome([transferIn], Today).Should().BeNull();
        SafeToSpend.NextIncome([In(10, 1_000)], Today).Should().BeNull();
        SafeToSpend.NextIncome([In(12, 1_000), In(11, 1_000)], Today).Should().Be(new DateTime(2026, 11, 11));
    }
}

public class PlanningSettingsTests
{
    [Fact]
    public void Safe_to_spend_defaults_to_the_next_income_and_saves()
    {
        var preferences = new FakePreferences();
        var settings = new PlanningSettings(preferences);
        settings.SafeToSpendUntil.Should().Be(SafeToSpendHorizon.NextIncome);

        settings.SafeToSpendUntil = SafeToSpendHorizon.EndOfMonth;

        new PlanningSettings(preferences).SafeToSpendUntil.Should().Be(SafeToSpendHorizon.EndOfMonth);
    }

    [Fact]
    public void An_unknown_saved_value_gives_the_default()
    {
        var preferences = new FakePreferences();
        preferences.Set("Planning.SafeToSpendUntil", "99");

        new PlanningSettings(preferences).SafeToSpendUntil.Should().Be(SafeToSpendHorizon.NextIncome);
    }
}
