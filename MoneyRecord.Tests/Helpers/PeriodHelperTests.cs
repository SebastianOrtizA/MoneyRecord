using AwesomeAssertions;
using MoneyRecord.Helpers;
using Xunit;

namespace MoneyRecord.Tests.Helpers;

public class PeriodHelperTests
{
    [Fact]
    public void A_whole_month_steps_by_month_and_a_whole_year_by_year()
    {
        PeriodHelper.StepOf(new DateTime(2026, 10, 1), new DateTime(2026, 10, 31, 23, 59, 59)).Should().Be(PeriodStep.Month);
        PeriodHelper.StepOf(new DateTime(2026, 2, 1), new DateTime(2026, 2, 28)).Should().Be(PeriodStep.Month);
        PeriodHelper.StepOf(new DateTime(2026, 1, 1), new DateTime(2026, 12, 31)).Should().Be(PeriodStep.Year);
    }

    [Fact]
    public void Other_ranges_have_no_step()
    {
        PeriodHelper.StepOf(new DateTime(2026, 10, 2), new DateTime(2026, 10, 31)).Should().BeNull("not from the 1st");
        PeriodHelper.StepOf(new DateTime(2026, 10, 1), new DateTime(2026, 10, 30)).Should().BeNull("not to the last day");
        PeriodHelper.StepOf(new DateTime(2026, 9, 1), new DateTime(2026, 10, 31)).Should().BeNull("two months");
        PeriodHelper.StepOf(new DateTime(2026, 3, 1), new DateTime(2027, 2, 28)).Should().BeNull("twelve months, not a calendar year");
        PeriodHelper.StepOf(default, default).Should().BeNull();
    }

    [Fact]
    public void Shifting_a_month_rolls_over_the_year_and_ends_on_the_last_tick()
    {
        var back = PeriodHelper.Shift(new DateTime(2026, 1, 1), PeriodStep.Month, -1);
        back.Start.Should().Be(new DateTime(2025, 12, 1));
        back.End.Should().Be(new DateTime(2026, 1, 1).AddTicks(-1));

        PeriodHelper.Shift(new DateTime(2026, 12, 1), PeriodStep.Month, 1).Start.Should().Be(new DateTime(2027, 1, 1));
    }

    [Fact]
    public void Shifting_covers_a_leap_february_and_a_year()
    {
        PeriodHelper.Shift(new DateTime(2024, 3, 1), PeriodStep.Month, -1).End.Date.Should().Be(new DateTime(2024, 2, 29));

        var year = PeriodHelper.Shift(new DateTime(2026, 1, 1), PeriodStep.Year, -1);
        year.Start.Should().Be(new DateTime(2025, 1, 1));
        year.End.Should().Be(new DateTime(2026, 1, 1).AddTicks(-1));
    }

    [Fact]
    public void Shifting_from_the_middle_of_a_month_gives_the_whole_month()
    {
        PeriodHelper.Shift(new DateTime(2026, 10, 17), PeriodStep.Month, 0).Start.Should().Be(new DateTime(2026, 10, 1));
    }
}
