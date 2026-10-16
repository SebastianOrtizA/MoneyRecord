using AwesomeAssertions;
using MoneyRecord.Helpers;
using Xunit;

namespace MoneyRecord.Tests.Helpers;

public class SplitAllocationTests
{
    [Fact]
    public void Remaining_is_the_total_minus_the_entered_parts()
    {
        SplitAllocation.Remaining(5_000, [3_000, null]).Should().Be(2_000);
        SplitAllocation.Remaining(5_000, [3_000, 2_500]).Should().Be(-500, "the parts are over the total");
    }

    [Fact]
    public void Share_follows_the_parts_and_adds_up_exactly()
    {
        SplitAllocation.Share(1_000, [3_000, 2_000]).Should().Equal(600, 400);
        // 1000 in thirds: 333.33 each, the leftover cent goes to the first
        SplitAllocation.Share(1_000, [1, 1, 1]).Should().Equal(334, 333, 333);
        // 5 cents over 3:1: 3.75 and 1.25 → the larger fraction gets the leftover
        SplitAllocation.Share(5, [3, 1]).Should().Equal(4, 1);
    }

    [Fact]
    public void Share_refuses_a_part_without_money()
    {
        FluentActions.Invoking(() => SplitAllocation.Share(100, [50, 0])).Should().Throw<ArgumentException>();
        SplitAllocation.Share(100, []).Should().BeEmpty();
    }
}
