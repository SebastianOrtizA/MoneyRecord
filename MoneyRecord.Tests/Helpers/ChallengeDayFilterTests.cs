using AwesomeAssertions;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using Xunit;

namespace MoneyRecord.Tests.Helpers;

public class ChallengeDayFilterTests
{
    private static readonly DateTime Start = new(2026, 10, 20);

    private static List<ChallengeDayInfo> Days(int count, params int[] done) =>
        Enumerable.Range(1, count).Select(i => new ChallengeDayInfo(i, Start.AddDays(i - 1), 100, done.Contains(i), null, false)).ToList();

    private static int[] Numbers(IEnumerable<ChallengeDayInfo> days) => days.Select(d => d.DayNumber).ToArray();

    [Fact]
    public void The_default_window_starts_at_the_first_day_not_done()
    {
        var days = Days(100, 1, 2, 3, 5);

        var view = ChallengeDayFilter.Select(days, ChallengeView.Next30, Start.AddDays(10), null, null, false);

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
        Numbers(ChallengeDayFilter.Select(Days(10), ChallengeView.GoalEnd, Start, null, null, false)).Should().HaveCount(10);
    }

    [Fact]
    public void Months_are_the_calendar_months_around_today()
    {
        var days = Days(90);
        var today = new DateTime(2026, 11, 15);

        Numbers(ChallengeDayFilter.Select(days, ChallengeView.ThisMonth, today, null, null, false)).Should().Equal(Enumerable.Range(13, 30));
        Numbers(ChallengeDayFilter.Select(days, ChallengeView.LastMonth, today, null, null, false)).Should().Equal(Enumerable.Range(1, 12));
        Numbers(ChallengeDayFilter.Select(days, ChallengeView.NextMonth, today, null, null, false)).Should().Equal(Enumerable.Range(43, 31));
    }

    [Fact]
    public void The_goals_last_30_days_are_the_end_of_the_plan()
    {
        Numbers(ChallengeDayFilter.Select(Days(365), ChallengeView.GoalEnd, Start, null, null, false)).Should().Equal(Enumerable.Range(336, 30));
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

        Numbers(ChallengeDayFilter.Select(days, ChallengeView.DateRange, Start, null, null, true)).Should().Equal(1, 3, 5, 6, 7, 8, 9, 10);
    }
}
