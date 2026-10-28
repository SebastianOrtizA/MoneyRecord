using System.Globalization;
using AwesomeAssertions;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using Xunit;

namespace MoneyRecord.Tests.Helpers;

public class ChallengeDayFilterTests
{
    // A Tuesday
    private static readonly DateTime Start = new(2026, 10, 20);

    private static List<ChallengeDayInfo> Days(int count, params int[] done) =>
        Enumerable.Range(1, count).Select(i => new ChallengeDayInfo(i, Start.AddDays(i - 1), 100, done.Contains(i), null, false)).ToList();

    private static int[] Numbers(IEnumerable<ChallengeDayInfo> days) => days.Select(d => d.DayNumber).ToArray();

    private static int?[] Slots(IEnumerable<ChallengeDayInfo?> slots) => slots.Select(s => s?.DayNumber).ToArray();

    /// <summary>The slots a month should have: this many empty ones, then these day numbers.</summary>
    private static int?[] Expect(int empty, IEnumerable<int> numbers) =>
        Enumerable.Repeat<int?>(null, empty).Concat(numbers.Select(n => (int?)n)).ToArray();

    [Fact]
    public void The_default_window_starts_at_the_first_day_not_done()
    {
        var days = Days(100, 1, 2, 3, 5);

        var view = ChallengeDayFilter.Select(days, ChallengeView.Next30, Start, null, null, false);

        Numbers(view).Should().Equal(Enumerable.Range(4, 30));
    }

    [Fact]
    public void The_default_window_of_a_finished_challenge_is_its_last_30_days()
    {
        var days = Days(40, Enumerable.Range(1, 40).ToArray());

        Numbers(ChallengeDayFilter.Select(days, ChallengeView.Next30, Start, null, null, false)).Should().Equal(Enumerable.Range(11, 30));
    }

    [Fact]
    public void A_pinned_start_keeps_the_window_still_when_the_first_day_is_marked()
    {
        var before = Days(100);
        var anchor = ChallengeDayFilter.FirstPendingDay(before);
        var after = Days(100, 1);

        anchor.Should().Be(1);
        Numbers(ChallengeDayFilter.Select(after, ChallengeView.Next30, Start, null, null, false, anchor)).Should().Equal(Enumerable.Range(1, 30));
        Numbers(ChallengeDayFilter.Select(after, ChallengeView.Next30, Start, null, null, false)).Should().Equal(Enumerable.Range(2, 30));
        ChallengeDayFilter.FirstPendingDay(after).Should().Be(2);
        ChallengeDayFilter.FirstPendingDay(Days(3, 1, 2, 3)).Should().BeNull();
    }

    [Fact]
    public void A_short_challenge_shows_all_its_days()
    {
        Numbers(ChallengeDayFilter.Select(Days(10), ChallengeView.Next30, Start, null, null, false)).Should().HaveCount(10);
    }

    [Fact]
    public void From_start_shows_every_day()
    {
        Numbers(ChallengeDayFilter.Select(Days(365), ChallengeView.FromStart, Start, null, null, false)).Should().Equal(Enumerable.Range(1, 365));
    }

    [Fact]
    public void A_month_is_the_days_in_that_calendar_month()
    {
        var days = Days(90);

        Numbers(ChallengeDayFilter.Select(days, ChallengeView.Month, new DateTime(2026, 11, 1), null, null, false)).Should().Equal(Enumerable.Range(13, 30));
        Numbers(ChallengeDayFilter.Select(days, ChallengeView.Month, new DateTime(2026, 10, 15), null, null, false)).Should().Equal(Enumerable.Range(1, 12));
        ChallengeDayFilter.Select(days, ChallengeView.Month, new DateTime(2026, 9, 1), null, null, false).Should().BeEmpty();
    }

    [Fact]
    public void A_date_range_is_inclusive_open_ended_and_may_be_given_backwards()
    {
        var days = Days(30);

        Numbers(ChallengeDayFilter.Select(days, ChallengeView.DateRange, Start, Start.AddDays(2), Start.AddDays(4), false)).Should().Equal(3, 4, 5);
        Numbers(ChallengeDayFilter.Select(days, ChallengeView.DateRange, Start, Start.AddDays(4), Start.AddDays(2), false)).Should().Equal(3, 4, 5);
        Numbers(ChallengeDayFilter.Select(days, ChallengeView.DateRange, Start, Start.AddDays(27), null, false)).Should().Equal(28, 29, 30);
        Numbers(ChallengeDayFilter.Select(days, ChallengeView.DateRange, Start, null, Start, false)).Should().Equal(1);
    }

    [Fact]
    public void Remaining_only_drops_the_days_done_from_any_view()
    {
        var days = Days(10, 2, 4);

        Numbers(ChallengeDayFilter.Select(days, ChallengeView.FromStart, Start, null, null, true)).Should().Equal(1, 3, 5, 6, 7, 8, 9, 10);
        Numbers(ChallengeDayFilter.Select(days, ChallengeView.DateRange, Start, null, null, true)).Should().Equal(1, 3, 5, 6, 7, 8, 9, 10);
    }

    [Fact]
    public void The_month_span_is_the_first_and_last_month_of_the_challenge()
    {
        ChallengeDayFilter.MonthSpan(Days(90)).Should().Be((new DateTime(2026, 10, 1), new DateTime(2027, 1, 1)));
        ChallengeDayFilter.MonthSpan(Days(5)).Should().Be((new DateTime(2026, 10, 1), new DateTime(2026, 10, 1)));
        ChallengeDayFilter.MonthSpan([]).Should().BeNull();
    }

    [Fact]
    public void A_month_is_moved_into_the_months_the_challenge_has()
    {
        var days = Days(90);

        ChallengeDayFilter.ClampMonth(days, new DateTime(2026, 9, 12)).Should().Be(new DateTime(2026, 10, 1));
        ChallengeDayFilter.ClampMonth(days, new DateTime(2027, 6, 30)).Should().Be(new DateTime(2027, 1, 1));
        ChallengeDayFilter.ClampMonth(days, new DateTime(2026, 11, 15)).Should().Be(new DateTime(2026, 11, 1));
        ChallengeDayFilter.ClampMonth([], new DateTime(2026, 11, 15)).Should().Be(new DateTime(2026, 11, 1));
    }

    [Fact]
    public void Month_slots_start_under_the_weekday_of_the_first_day()
    {
        var days = Days(90);
        var october = new DateTime(2026, 10, 1);

        // Day 1 is a Tuesday: one empty slot in a Monday-first week, two in a Sunday-first one
        Slots(ChallengeDayFilter.MonthSlots(days, october, false, DayOfWeek.Monday)).Should().Equal(Expect(1, Enumerable.Range(1, 12)));
        Slots(ChallengeDayFilter.MonthSlots(days, october, false, DayOfWeek.Sunday)).Should().Equal(Expect(2, Enumerable.Range(1, 12)));
        // November 1 is a Sunday: the last slot of a Monday-first week, the first of a Sunday-first one
        Slots(ChallengeDayFilter.MonthSlots(days, new DateTime(2026, 11, 1), false, DayOfWeek.Monday)).Take(7).Should().Equal(Expect(6, [13]));
        Slots(ChallengeDayFilter.MonthSlots(days, new DateTime(2026, 11, 1), false, DayOfWeek.Sunday)).Take(2).Should().Equal(Expect(0, [13, 14]));
    }

    [Fact]
    public void Remaining_only_leaves_empty_slots_so_the_weekdays_stay_lined_up()
    {
        var days = Days(90, 1, 2, 12);

        var slots = ChallengeDayFilter.MonthSlots(days, new DateTime(2026, 10, 1), true, DayOfWeek.Monday);

        // The slot before day 1, the two done days, then days 3-11; day 12 is done and trails off
        Slots(slots).Should().Equal(Expect(3, Enumerable.Range(3, 9)));
    }

    [Fact]
    public void A_month_with_nothing_to_show_has_no_slots()
    {
        var days = Days(90, Enumerable.Range(1, 12).ToArray());

        ChallengeDayFilter.MonthSlots(days, new DateTime(2026, 10, 1), true, DayOfWeek.Monday).Should().BeEmpty();
        ChallengeDayFilter.MonthSlots(days, new DateTime(2026, 8, 1), false, DayOfWeek.Monday).Should().BeEmpty();
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("es-ES")]
    [InlineData("fr-FR")]
    public void Weekday_names_start_at_the_first_day_of_the_week(string name)
    {
        var culture = CultureInfo.GetCultureInfo(name);

        var headers = ChallengeDayFilter.WeekdayHeaders(culture);

        headers.Should().HaveCount(7);
        headers[0].Should().Be(culture.DateTimeFormat.ShortestDayNames[(int)culture.DateTimeFormat.FirstDayOfWeek]);
        headers.Should().BeEquivalentTo(culture.DateTimeFormat.ShortestDayNames);
    }
}
