using AwesomeAssertions;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using Xunit;

namespace MoneyRecord.Tests.Helpers;

public class RecurrenceCalculatorTests
{
    [Theory]
    [InlineData(RecurrenceFrequency.Daily, "2026-03-11")]
    [InlineData(RecurrenceFrequency.Weekly, "2026-03-17")]
    [InlineData(RecurrenceFrequency.Biweekly, "2026-03-24")]
    [InlineData(RecurrenceFrequency.Monthly, "2026-04-10")]
    [InlineData(RecurrenceFrequency.Yearly, "2027-03-10")]
    public void Next_adds_one_period_and_drops_the_time_of_day(RecurrenceFrequency frequency, string expected)
    {
        RecurrenceCalculator.Next(new DateTime(2026, 3, 10, 15, 45, 0), frequency)
            .Should().Be(DateTime.Parse(expected, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void A_day_of_month_beyond_the_month_end_uses_the_last_day_and_comes_back_after()
    {
        var jan31 = new DateTime(2026, 1, 31);

        var feb = RecurrenceCalculator.Next(jan31, RecurrenceFrequency.Monthly, 31);
        var mar = RecurrenceCalculator.Next(feb, RecurrenceFrequency.Monthly, 31);

        feb.Should().Be(new DateTime(2026, 2, 28));
        mar.Should().Be(new DateTime(2026, 3, 31));
    }

    [Fact]
    public void The_day_of_month_only_applies_to_monthly()
    {
        RecurrenceCalculator.Next(new DateTime(2026, 3, 10), RecurrenceFrequency.Weekly, 31)
            .Should().Be(new DateTime(2026, 3, 17));
    }

    [Fact]
    public void NextAfter_uses_this_months_day_when_it_is_still_ahead()
    {
        RecurrenceCalculator.NextAfter(new DateTime(2026, 3, 10, 9, 0, 0), RecurrenceFrequency.Monthly, 25)
            .Should().Be(new DateTime(2026, 3, 25));
    }

    [Fact]
    public void NextAfter_moves_to_next_month_when_the_day_is_today_or_past()
    {
        RecurrenceCalculator.NextAfter(new DateTime(2026, 3, 25, 9, 0, 0), RecurrenceFrequency.Monthly, 25)
            .Should().Be(new DateTime(2026, 4, 25));
        RecurrenceCalculator.NextAfter(new DateTime(2026, 1, 31, 9, 0, 0), RecurrenceFrequency.Monthly, 31)
            .Should().Be(new DateTime(2026, 2, 28));
    }

    [Fact]
    public void NextAfter_without_a_day_is_one_period_from_today()
    {
        RecurrenceCalculator.NextAfter(new DateTime(2026, 3, 10, 9, 0, 0), RecurrenceFrequency.Weekly)
            .Should().Be(new DateTime(2026, 3, 17));
    }
}
