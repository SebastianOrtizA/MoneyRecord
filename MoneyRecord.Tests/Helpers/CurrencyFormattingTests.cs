using FluentAssertions;
using MoneyRecord.Behaviors;
using MoneyRecord.Helpers;
using MoneyRecord.Services;
using System.Globalization;
using Xunit;

namespace MoneyRecord.Tests.Helpers;

/// <summary>
/// Formatting and parsing in an explicit currency (no test here depends on the app-wide base currency).
/// </summary>
public class CurrencyFormattingTests
{
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
        MoneyFormatter.Format(1500m, "JPY", new CultureInfo("en-US")).Should().Be("¥1,500");
    }

    [Fact]
    public void Cop_InEnUs_UsesAnUnambiguousSymbol()
    {
        MoneyFormatter.Format(-50m, "COP", new CultureInfo("en-US")).Should().Be("-COL$50.00");
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

    [Fact]
    public void ParseAmount_SymbolWithDot_IsNotADecimalSeparator()
    {
        WithCulture("en-US", () =>
            CurrencyMaskBehavior.ParseAmount("S/.1,250.50", "PEN").Should().Be(1250.50m));
    }

    [Fact]
    public void ParseAmount_ZeroDecimalCurrency_RoundsToWholeUnits()
    {
        WithCulture("en-US", () =>
            CurrencyMaskBehavior.ParseAmount("¥1,500", "JPY").Should().Be(1500m));
    }

    [Fact]
    public void ParseAmount_SpanishDecimalComma()
    {
        WithCulture("es-CO", () =>
            CurrencyMaskBehavior.ParseAmount("12,50", "COP").Should().Be(12.50m));
    }

    [Fact]
    public void ParseAmount_RoundTripsFormattedText()
    {
        WithCulture("es-ES", () =>
        {
            var text = MoneyFormatter.Format(1234.56m, "EUR");
            CurrencyMaskBehavior.ParseAmount(text, "EUR").Should().Be(1234.56m);
        });
    }

    private static void WithCulture(string name, Action action)
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo(name);
        try { action(); }
        finally { CultureInfo.CurrentCulture = previous; }
    }
}
