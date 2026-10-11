using AwesomeAssertions;
using MoneyRecord.Helpers;
using Xunit;
using static MoneyRecord.Tests.TestCulture;

namespace MoneyRecord.Tests.Helpers;

/// <summary>
/// Parsing amounts typed or shown in an explicit currency.
/// </summary>
public class AmountParserTests
{
    [Fact]
    public void Parse_SymbolWithDot_IsNotADecimalSeparator()
    {
        WithCulture("en-US", () =>
            AmountParser.Parse("S/.1,250.50", "PEN").Should().Be(1250.50m));
    }

    [Fact]
    public void Parse_ZeroDecimalCurrency_RoundsToWholeUnits()
    {
        WithCulture("en-US", () =>
            AmountParser.Parse("¥1,500", "JPY").Should().Be(1500m));
    }

    [Fact]
    public void Parse_SpanishDecimalComma()
    {
        WithCulture("es-CO", () =>
            AmountParser.Parse("12,50", "COP").Should().Be(12.50m));
    }

    [Fact]
    public void Parse_RoundTripsFormattedText()
    {
        WithCulture("es-ES", () =>
        {
            var text = MoneyFormatter.Format(1234.56m, "EUR");
            AmountParser.Parse(text, "EUR").Should().Be(1234.56m);
        });
    }
}
