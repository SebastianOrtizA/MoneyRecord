using AwesomeAssertions;
using MoneyRecord.Helpers;
using System.Globalization;
using Xunit;

namespace MoneyRecord.Tests.Helpers;

/// <summary>Formatting with USD as the base currency (restored afterwards).</summary>
public class MoneyFormatterTests : IDisposable
{
    private readonly string _previousBase = MoneyFormatter.BaseCurrencyCode;

    public MoneyFormatterTests() => MoneyFormatter.BaseCurrencyCode = "USD";

    public void Dispose() => MoneyFormatter.BaseCurrencyCode = _previousBase;

    [Fact]
    public void EnUs_Negative_PutsSignBeforeSymbol()
    {
        MoneyFormatter.Format(-50m, "USD", new CultureInfo("en-US")).Should().Be("-$50.00");
    }

    [Fact]
    public void EnUs_Positive_HasNoSign()
    {
        MoneyFormatter.Format(1250.5m, "USD", new CultureInfo("en-US")).Should().Be("$1,250.50");
    }

    [Theory]
    [InlineData(5, "+$5.00")]
    [InlineData(-5, "-$5.00")]
    [InlineData(0, "$0.00")]
    public void FormatSigned_PutsAPlusOnlyOnPositiveAmounts(int amount, string expected)
    {
        MoneyFormatter.FormatSigned(amount, "USD", new CultureInfo("en-US")).Should().Be(expected);
    }

    [Fact]
    public void EsCo_UsesPesoSymbolAndLocalSeparators()
    {
        MoneyFormatter.Format(-50000m, "COP", new CultureInfo("es-CO")).Should().Be("-$50.000,00 COP");
    }

    [Fact]
    public void EsEs_PutsTheSymbolInFrontToo()
    {
        MoneyFormatter.Format(-50m, "EUR", new CultureInfo("es-ES")).Should().Be("-€50,00");
        MoneyFormatter.Format(1401.75m, "USD", new CultureInfo("es-ES")).Should().Be("$1.401,75");
        MoneyFormatter.Format(300m, "COP", new CultureInfo("es-ES")).Should().Be("$300,00 COP");
    }

    [Fact]
    public void A_shared_symbol_gets_the_code_only_outside_the_base_currency()
    {
        var english = new CultureInfo("en-US");
        MoneyFormatter.Format(5m, "USD", english).Should().Be("$5.00");
        MoneyFormatter.Format(5m, "CAD", english).Should().Be("$5.00 CAD");
        MoneyFormatter.Format(5m, "GBP", english).Should().Be("£5.00");

        MoneyFormatter.BaseCurrencyCode = "COP";
        MoneyFormatter.Format(5m, "COP", english).Should().Be("$5.00");
        MoneyFormatter.Format(5m, "USD", english).Should().Be("$5.00 USD");
    }

    [Fact]
    public void JaJp_HasNoDecimals()
    {
        MoneyFormatter.Format(-500m, "JPY", new CultureInfo("ja-JP")).Should().Be("-¥500 JPY");
    }

    [Fact]
    public void NeutralCulture_NeverShowsGenericCurrencySign()
    {
        MoneyFormatter.Format(10m, culture: new CultureInfo("es")).Should().NotContain("¤");
    }
}
