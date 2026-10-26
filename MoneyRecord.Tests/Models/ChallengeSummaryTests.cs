using AwesomeAssertions;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using Xunit;

namespace MoneyRecord.Tests.Models;

public class ChallengeSummaryTests
{
    private static ChallengeSummary Summary(params bool[] done)
    {
        var challenge = new SavingsChallenge { Name = "Hucha", CurrencyCode = "USD", Days = done.Length };
        var days = done.Select((d, i) => new ChallengeDayInfo(i + 1, new DateTime(2026, 10, 1).AddDays(i), 1_000, d, null, false)).ToList();
        return new ChallengeSummary(challenge, days, days.Where(d => d.IsDone).Sum(d => d.AmountCents), days.Sum(d => d.AmountCents), DaysLeft: 2);
    }

    [Fact]
    public void Fraction_is_saved_over_total_and_counts_the_days_done()
    {
        var summary = Summary(true, false, false, true);

        summary.Fraction.Should().BeApproximately(0.5, 1e-9);
        summary.DoneCount.Should().Be(2);
        summary.IsFinished.Should().BeFalse();
        summary.DaysLeftText.Should().Be(Plurals.ChallengeDaysLeft(2));
    }

    [Fact]
    public void A_challenge_with_every_day_done_is_finished()
    {
        var summary = Summary(true, true);

        summary.IsFinished.Should().BeTrue();
        summary.DaysLeftText.Should().Be(AppResources.ChallengeFinished);
        summary.Fraction.Should().Be(1);
    }

    [Fact]
    public void Saved_of_text_shows_both_amounts_in_the_challenge_currency()
    {
        var text = Summary(true, false).SavedOfText;

        text.Should().Contain("10").And.Contain("20");
    }
}
