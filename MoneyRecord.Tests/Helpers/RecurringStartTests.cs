using AwesomeAssertions;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using Xunit;

namespace MoneyRecord.Tests.Helpers;

public sealed class RecurringStartTests
{
    private static readonly DateTime Today = new(2026, 10, 17, 15, 30, 0);

    [Fact]
    public void Today_records_the_first_entry_now_and_goes_on_from_the_next_period()
    {
        var start = RecurringStart.For(Today, Today, RecurrenceFrequency.Monthly, dayOfMonth: null);

        start.Should().Be(new RecurringStart(true, new DateTime(2026, 11, 17), Today));
    }

    [Fact]
    public void A_past_date_records_the_first_entry_on_that_date()
    {
        var start = RecurringStart.For(new DateTime(2026, 10, 1), Today, RecurrenceFrequency.Weekly, dayOfMonth: null);

        start.Should().Be(new RecurringStart(true, new DateTime(2026, 10, 8), new DateTime(2026, 10, 1)));
    }

    [Fact]
    public void A_future_date_saves_only_the_schedule_starting_on_that_day()
    {
        var start = RecurringStart.For(new DateTime(2026, 11, 3, 9, 45, 0), Today, RecurrenceFrequency.Monthly, dayOfMonth: 5);

        start.Should().Be(new RecurringStart(false, new DateTime(2026, 11, 3), null));
    }

    [Fact]
    public void Tomorrow_is_the_earliest_schedule_date_and_the_first_future_one()
    {
        var tomorrow = RecurringStart.EarliestScheduleDate(Today);

        tomorrow.Should().Be(new DateTime(2026, 10, 18));
        RecurringStart.IsFuture(tomorrow, Today).Should().BeTrue();
        RecurringStart.IsFuture(Today.Date.AddHours(23), Today).Should().BeFalse();
    }
}
