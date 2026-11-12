using AwesomeAssertions;
using MoneyRecord.Helpers;
using System.Globalization;
using Xunit;

namespace MoneyRecord.Tests.Helpers;

public class AccountMoveRateTests
{
    [Theory]
    [InlineData(350, 128.5, 44975)]   // 3.50 x 128.5 = 449.75
    [InlineData(100, 0.5, 50)]
    [InlineData(-100, 0.5, -50)]
    [InlineData(1, 0.5, 1)]           // half a cent rounds away from zero
    [InlineData(-1, 0.5, -1)]
    public void Convert_rounds_to_whole_cents_away_from_zero(long cents, double rate, long expected)
    {
        AccountMoveRate.Convert(cents, (decimal)rate).Should().Be(expected);
    }

    [Fact]
    public void The_final_amount_and_the_rate_give_each_other_back()
    {
        const long net = 350; // 3.50 in the old currency
        var rate = AccountMoveRate.RateFor(net, 449.75m);

        rate.Should().Be(128.5m);
        AccountMoveRate.FinalFor(net, rate!.Value).Should().Be(449.75m);
    }

    [Fact]
    public void Signs_are_left_out_both_ways()
    {
        AccountMoveRate.RateFor(-350, 449.75m).Should().Be(128.5m);
        AccountMoveRate.FinalFor(-350, 128.5m).Should().Be(449.75m);
    }

    [Theory]
    [InlineData(0, 100)]      // nothing to divide
    [InlineData(350, 0)]      // worth nothing
    [InlineData(350, -5)]
    public void Without_a_net_or_a_final_amount_there_is_no_rate(long net, double final)
    {
        AccountMoveRate.RateFor(net, (decimal)final).Should().BeNull();
    }

    [Theory]
    [InlineData("en-US", "128.5", 128.5)]
    [InlineData("en-US", " 4012.5 ", 4012.5)]
    [InlineData("en-US", "128,5", 128.5)]      // a comma still works as the decimal mark
    [InlineData("es-ES", "128,5", 128.5)]
    [InlineData("es-ES", "128.5", 128.5)]      // never 1285: a rate has no thousands separator
    [InlineData("es-ES", "0.000123", 0.000123)]
    public void A_typed_rate_is_read_in_any_culture(string culture, string text, double expected)
    {
        AccountMoveRate.ParseRate(text, new CultureInfo(culture)).Should().Be((decimal)expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-3")]
    [InlineData("1,000.5")]
    public void Anything_but_a_positive_number_is_no_rate(string? text)
    {
        AccountMoveRate.ParseRate(text, new CultureInfo("en-US")).Should().BeNull();
    }

    [Fact]
    public void A_rate_is_shown_without_group_separators_or_trailing_zeros()
    {
        AccountMoveRate.FormatRate(4012.5m, new CultureInfo("en-US")).Should().Be("4012.5");
        AccountMoveRate.FormatRate(4012.5m, new CultureInfo("es-ES")).Should().Be("4012,5");
        AccountMoveRate.FormatRate(2m, new CultureInfo("en-US")).Should().Be("2");
    }
}
