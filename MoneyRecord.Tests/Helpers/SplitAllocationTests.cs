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
    public void Complete_gives_the_one_empty_part_what_is_left()
    {
        SplitAllocation.Complete(5_000, [3_000, null]).Problem.Should().Be(SplitProblem.None);
        SplitAllocation.Complete(5_000, [3_000, null]).Cents.Should().Equal(3_000, 2_000);
        SplitAllocation.Complete(5_000, [1_000, null, 1_500]).Cents.Should().Equal(1_000, 2_500, 1_500);
        SplitAllocation.Complete(5_000, [3_000, 2_000]).Cents.Should().Equal(3_000, 2_000);
    }

    [Theory]
    [InlineData(5_000L, new long[] { 3_000, -1, -1 }, SplitProblem.MissingAmount)] // two empty parts: nothing to guess
    [InlineData(5_000L, new long[] { 5_000, -1 }, SplitProblem.MissingAmount)]     // nothing left for the empty one
    [InlineData(5_000L, new long[] { 6_000, -1 }, SplitProblem.DoesNotAddUp)]      // the others are over already
    [InlineData(5_000L, new long[] { 3_000, 0 }, SplitProblem.MissingAmount)]
    [InlineData(5_000L, new long[] { 3_000, 1_000 }, SplitProblem.DoesNotAddUp)]
    [InlineData(5_000L, new long[] { 3_000, 3_000 }, SplitProblem.DoesNotAddUp)]
    public void Complete_names_what_to_fix(long total, long[] parts, SplitProblem expected)
    {
        // -1 stands for an empty part
        var completion = SplitAllocation.Complete(total, parts.Select(p => p == -1 ? (long?)null : p).ToList());

        completion.Problem.Should().Be(expected);
        completion.Cents.Should().BeNull();
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
