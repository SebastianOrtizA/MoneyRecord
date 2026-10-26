using AwesomeAssertions;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using Xunit;

namespace MoneyRecord.Tests.Helpers;

public class ChallengePlanTests
{
    [Fact]
    public void Doubling_ten_days_for_1000_starts_under_one_and_adds_up()
    {
        var plan = ChallengePlan.Build(100_000, 10, ChallengeMode.Doubling, 0);

        plan.DayCents[0].Should().Be(98);
        plan.DayCents.Zip(plan.DayCents.Skip(1)).Should().OnlyContain(p => p.Second >= p.First);
        plan.TotalCents.Should().BeInRange(99_900, 100_100);
        plan.UsesLevels.Should().BeFalse();
        plan.MaxUsefulDoublings.Should().Be(10);
    }

    [Fact]
    public void Doubling_more_days_than_doublings_uses_levels_that_last_several_days()
    {
        var plan = ChallengePlan.Build(100_000, 30, ChallengeMode.Doubling, 0);

        plan.UsesLevels.Should().BeTrue();
        plan.DayCents.Distinct().Should().HaveCount(plan.MaxUsefulDoublings);
        plan.DayCents.Zip(plan.DayCents.Skip(1)).Should().OnlyContain(p => p.Second >= p.First);
        plan.TotalCents.Should().BeInRange(99_000, 101_000);
        ChallengePlan.Build(100_000, 32, ChallengeMode.Doubling, 0).DayCents.GroupBy(c => c).Select(g => g.Count()).Should().Equal(4, 4, 3, 3, 3, 3, 3, 3, 3, 3);
        // 30 days over 10 levels: three days each
        plan.DayCents.GroupBy(c => c).Select(g => g.Count()).Should().Equal(Enumerable.Repeat(3, 10));
    }

    [Fact]
    public void Linear_day_i_saves_i_times_the_step()
    {
        var plan = ChallengePlan.Build(46_500, 30, ChallengeMode.Linear, 0);

        // 465 = 30 * 31 / 2, so the step is exactly 1.00
        plan.DayCents.Should().Equal(Enumerable.Range(1, 30).Select(i => i * 100L));
        plan.TotalCents.Should().Be(46_500);
    }

    [Fact]
    public void Rounding_to_a_step_makes_every_day_a_multiple_and_shows_the_difference()
    {
        var plan = ChallengePlan.Build(100_000, 30, ChallengeMode.Linear, 1_000);

        plan.DayCents.Should().OnlyContain(c => c % 1_000 == 0 && c >= 1_000);
        plan.TotalCents.Should().NotBe(100_000);
    }

    [Fact]
    public void One_day_saves_the_whole_goal()
    {
        ChallengePlan.Build(50_000, 1, ChallengeMode.Doubling, 0).DayCents.Should().Equal(50_000);
        ChallengePlan.Build(50_000, 1, ChallengeMode.Linear, 0).DayCents.Should().Equal(50_000);
    }

    [Fact]
    public void A_zero_decimal_currency_rounds_to_whole_units()
    {
        // JPY is stored on the same scale of 100: rounding to 1 unit = 100
        var plan = ChallengePlan.Build(100_000_000, 20, ChallengeMode.Doubling, 100);

        plan.DayCents.Should().OnlyContain(c => c % 100 == 0);
    }

    [Fact]
    public void A_huge_day_count_still_builds_with_every_day_at_least_one_step()
    {
        var plan = ChallengePlan.Build(100_000, 5_000, ChallengeMode.Doubling, 0);

        plan.DayCents.Should().HaveCount(5_000).And.OnlyContain(c => c >= 1);
        plan.UsesLevels.Should().BeTrue();
    }

    [Fact]
    public void Equal_splits_the_goal_over_the_days()
    {
        var plan = ChallengePlan.Build(100_000, 4, ChallengeMode.Equal, 0);

        plan.DayCents.Should().Equal(25_000, 25_000, 25_000, 25_000);
        plan.TotalCents.Should().Be(100_000);
    }

    [Fact]
    public void Equal_rounds_each_day_up_to_the_step_and_the_last_day_takes_the_rest()
    {
        // 1,000,000 / 30 = 33,333.33 → 33,350 with a step of 50; the 30th day is what is left
        var plan = ChallengePlan.Build(100_000_000, 30, ChallengeMode.Equal, 5_000);

        plan.DayCents.Take(29).Should().OnlyContain(c => c == 3_335_000);
        plan.DayCents[^1].Should().Be(100_000_000 - 29 * 3_335_000);
        plan.TotalCents.Should().Be(100_000_000);
    }

    [Fact]
    public void A_fixed_daily_amount_sets_the_days_and_the_last_day_is_the_remainder()
    {
        DaysFor(103_200_000, 500_000).Should().Be(207);
        var plan = ChallengePlan.Build(103_200_000, 207, ChallengeMode.Equal, 0, 500_000);

        plan.DayCents.Take(206).Should().OnlyContain(c => c == 500_000);
        plan.DayCents[^1].Should().Be(200_000);
        plan.TotalCents.Should().Be(103_200_000);

        static int DaysFor(long goal, long amount) => ChallengePlan.DaysFor(goal, amount);
    }

    [Theory]
    [InlineData(500)]
    [InlineData(5_000)]
    [InlineData(50_000)]
    public void Linear_with_a_coin_step_makes_every_day_a_multiple_of_it(long step)
    {
        var plan = ChallengePlan.Build(103_200_000, 60, ChallengeMode.Linear, step);

        plan.DayCents.Should().OnlyContain(c => c % step == 0 && c >= step);
    }

    [Fact]
    public void Zero_days_is_refused()
    {
        var act = () => ChallengePlan.Build(100_000, 0, ChallengeMode.Linear, 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
