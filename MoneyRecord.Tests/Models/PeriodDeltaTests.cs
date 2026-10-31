using AwesomeAssertions;
using MoneyRecord.Models;
using Xunit;

namespace MoneyRecord.Tests.Models;

public class PeriodDeltaTests
{
    [Theory]
    [InlineData(150, 100, false, DeltaTone.Bad)]
    [InlineData(50, 100, false, DeltaTone.Good)]
    [InlineData(150, 100, true, DeltaTone.Good)]
    [InlineData(50, 100, true, DeltaTone.Bad)]
    [InlineData(100, 100, false, DeltaTone.Neutral)]
    [InlineData(100, 0, false, DeltaTone.Neutral)]
    public void Tone_depends_on_the_direction_and_on_what_counts_as_good(int current, int previous, bool higherIsBetter, DeltaTone expected)
    {
        new PeriodDelta(current, previous).ToneFor(higherIsBetter).Should().Be(expected);
    }
}
