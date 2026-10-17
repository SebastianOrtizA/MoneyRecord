using AwesomeAssertions;
using MoneyRecord.Helpers;
using MoneyRecord.Services;
using System.Globalization;
using Xunit;
using static MoneyRecord.Tests.TestCulture;

namespace MoneyRecord.Tests.Helpers;

/// <summary>
/// Formatting in an explicit currency, with USD as the base currency (restored afterwards).
/// </summary>
public class CurrencyFormattingTests : IDisposable
{
    private readonly string _previousBase = MoneyFormatter.BaseCurrencyCode;

    public CurrencyFormattingTests() => MoneyFormatter.BaseCurrencyCode = "USD";

    public void Dispose() => MoneyFormatter.BaseCurrencyCode = _previousBase;

    [Theory]
    [InlineData("JPY", 0)]
    [InlineData("clp", 0)]
    [InlineData("KRW", 0)]
    [InlineData("USD", 2)]
    [InlineData("COP", 2)]
    public void GetMinorUnits(string code, int expected)
    {
        CurrencyService.GetMinorUnits(code).Should().Be(expected);
    }

    [Fact]
    public void Jpy_InEnUs_HasNoDecimalsAndYenSign()
    {
        MoneyFormatter.Format(1500m, "JPY", new CultureInfo("en-US")).Should().Be("¥1,500 JPY");
    }

    [Fact]
    public void Cop_InEnUs_UsesThePlainSignAndTheCode()
    {
        MoneyFormatter.Format(-50m, "COP", new CultureInfo("en-US")).Should().Be("-$50.00 COP");
    }

    [Fact]
    public void Eur_InEnUs_UsesEuroSign()
    {
        MoneyFormatter.Format(300m, "EUR", new CultureInfo("en-US")).Should().Be("€300.00");
    }

    [Fact]
    public void LowercaseCode_IsTheSameCurrency()
    {
        MoneyFormatter.Format(1m, "eur", new CultureInfo("de-DE"))
            .Should().Be(MoneyFormatter.Format(1m, "EUR", new CultureInfo("de-DE")));
    }

    [Fact]
    public void UnknownCurrency_ShowsItsCode()
    {
        MoneyFormatter.Format(5m, "XYZ", new CultureInfo("en-US")).Should().Contain("XYZ");
    }

    [Fact]
    public void FormatWhole_DropsDecimals()
    {
        MoneyFormatter.FormatWhole(1234m, "USD", new CultureInfo("en-US")).Should().Be("$1,234");
    }

    [Theory]
    [InlineData("COP", 6000300.5, "en-US", "$6,000,300.50 COP")]
    [InlineData("CAD", -12, "en-US", "-$12.00 CAD")]
    [InlineData("USD", 1200, "en-US", "$1,200.00 USD")]
    [InlineData("EUR", 300, "en-US", "€300.00 EUR")]
    [InlineData("JPY", 1500, "en-US", "¥1,500 JPY")]
    [InlineData("CHF", 5, "en-US", "CHF 5.00")]
    [InlineData("XYZ", 5, "en-US", "XYZ 5.00")]
    public void FormatWithCode_UsesTheBareSignThenTheCode(string code, double amount, string culture, string expected)
    {
        MoneyFormatter.FormatWithCode((decimal)amount, code, new CultureInfo(culture))
            .Should().Be(expected);
    }

    [Theory]
    [InlineData("USD", 1401.75, "es-ES", "$1.401,75 USD")]
    [InlineData("EUR", 300, "es-ES", "€300,00 EUR")]
    [InlineData("COP", -1200, "es-ES", "-$1.200,00 COP")]
    [InlineData("COP", 1200, "es-CO", "$1.200,00 COP")]
    public void FormatWithCode_PutsTheSignFirstWhereverTheCultureWritesIt(string code, double amount, string culture, string expected)
    {
        MoneyFormatter.FormatWithCode((decimal)amount, code, new CultureInfo(culture)).Should().Be(expected);
    }

    [Theory]
    [InlineData("COP", 6000300, "es-ES", "$6.000.300,00")]
    [InlineData("USD", -12.5, "es-ES", "-$12,50")]
    [InlineData("COP", 300, "en-US", "$300.00")]
    [InlineData("CHF", 5, "en-US", "CHF 5.00")]
    [InlineData("CHF", -5, "en-US", "-CHF 5.00")]
    [InlineData("SEK", 5, "es-ES", "kr 5,00")]
    [InlineData("PLN", 5, "es-ES", "zł 5,00")]
    [InlineData("PEN", 5, "en-US", "S/. 5.00")]
    [InlineData("ZAR", 5, "en-US", "R 5.00")]
    [InlineData("BRL", 5, "en-US", "R$5.00")]
    public void FormatBesideCode_IsTheSameFormWithoutTheCode(string code, double amount, string culture, string expected)
    {
        MoneyFormatter.FormatBesideCode((decimal)amount, code, new CultureInfo(culture)).Should().Be(expected);
    }

    [Fact]
    public void SameCurrency_TreatsEmptyAsTheBaseAndIgnoresCase()
    {
        MoneyFormatter.SameCurrency("usd", "USD").Should().BeTrue();
        MoneyFormatter.SameCurrency("", MoneyFormatter.BaseCurrency).Should().BeTrue();
        MoneyFormatter.SameCurrency("EUR", "USD").Should().BeFalse();
    }
}
