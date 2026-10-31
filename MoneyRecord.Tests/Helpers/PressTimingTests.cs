using AwesomeAssertions;
using MoneyRecord.Helpers;
using Xunit;

namespace MoneyRecord.Tests.Helpers;

public class PressTimingTests
{
    private static readonly DateTime Now = new(2026, 10, 31, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(100, false)]
    [InlineData(400, false)]
    [InlineData(401, true)]
    [InlineData(1500, true)]
    public void A_press_held_past_the_threshold_is_a_hold(int heldMs, bool expected)
    {
        PressTiming.IsHold(Now.AddMilliseconds(-heldMs), Now).Should().Be(expected);
    }

    [Fact]
    public void Without_a_known_touch_down_it_is_a_tap()
    {
        PressTiming.IsHold(null, Now).Should().BeFalse();
    }

    [Fact]
    public void A_hold_may_clear_unless_a_newer_press_began_after_the_release()
    {
        PressTiming.MayClearAfterRelease(Now.AddSeconds(-2), Now).Should().BeTrue();
        PressTiming.MayClearAfterRelease(null, Now).Should().BeTrue();
        PressTiming.MayClearAfterRelease(Now.AddMilliseconds(300), Now).Should().BeFalse();
    }
}
