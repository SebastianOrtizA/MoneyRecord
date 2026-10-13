using AwesomeAssertions;
using MoneyRecord.Helpers;
using Xunit;

namespace MoneyRecord.Tests.Helpers;

public class PickListTests
{
    [Fact]
    public void UniqueLabels_numbers_repeated_names()
    {
        PickList.UniqueLabels(["Other", "Food", "Other", "Other"])
            .Should().Equal("Other", "Food", "Other (2)", "Other (3)");
    }
}
