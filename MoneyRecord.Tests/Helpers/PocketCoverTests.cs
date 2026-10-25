using AwesomeAssertions;
using MoneyRecord.Helpers;
using Xunit;

namespace MoneyRecord.Tests.Helpers;

public sealed class PocketCoverTests
{
    [Fact]
    public void A_pocket_that_holds_enough_covers_everything()
    {
        PocketCover.Plan(100m, 500m).Should().Be((100m, 0m));
    }

    [Fact]
    public void A_smaller_pocket_gives_all_it_has_and_the_rest_is_still_short()
    {
        PocketCover.Plan(300m, 200m).Should().Be((200m, 100m));
    }

    [Fact]
    public void An_empty_pocket_covers_nothing()
    {
        PocketCover.Plan(50m, 0m).Should().Be((0m, 50m));
    }
}
