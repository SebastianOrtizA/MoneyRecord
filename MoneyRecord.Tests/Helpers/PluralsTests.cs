using AwesomeAssertions;
using MoneyRecord.Helpers;
using MoneyRecord.Services;
using Xunit;

namespace MoneyRecord.Tests.Helpers;

public class PluralsTests
{
    [Fact]
    public void One_UsesSingular()
    {
        Plurals.Transactions(1).Should().Be(string.Format(LocalizationService.Instance.GetString("TransactionCountOne"), 1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(11)]
    public void OtherCounts_UsePlural(int count)
    {
        Plurals.Transactions(count).Should().Be(string.Format(LocalizationService.Instance.GetString("TransactionCountOther"), count));
    }
}
