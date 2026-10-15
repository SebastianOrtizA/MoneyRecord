using AwesomeAssertions;
using MoneyRecord.Helpers;
using System.Globalization;
using Xunit;

namespace MoneyRecord.Tests.Helpers;

public class TransferRateTests
{
    private static readonly CultureInfo EnUs = new("en-US");

    [Fact]
    public void Shows_one_unit_of_the_currency_worth_more_whichever_way_the_money_went()
    {
        TransferRate.Describe(100m, "USD", 401_250m, "COP", EnUs).Should().Be("1 USD = 4,012.5 COP");
        TransferRate.Describe(401_250m, "COP", 100m, "USD", EnUs).Should().Be("1 USD = 4,012.5 COP");
    }

    [Fact]
    public void Rounds_to_four_decimals()
    {
        TransferRate.Describe(300m, "EUR", 322.5m, "USD", EnUs).Should().Be("1 EUR = 1.075 USD");
        TransferRate.Describe(3m, "EUR", 10m, "USD", EnUs).Should().Be("1 EUR = 3.3333 USD");
    }

    [Theory]
    [InlineData(0, 5)]
    [InlineData(5, 0)]
    public void Is_null_without_both_amounts(int sent, int received)
    {
        TransferRate.Describe(sent, "EUR", received, "USD", EnUs).Should().BeNull();
    }
}
