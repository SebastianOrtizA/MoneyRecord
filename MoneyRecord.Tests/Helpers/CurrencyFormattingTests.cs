using AwesomeAssertions;
using MoneyRecord.Helpers;
using MoneyRecord.Services;
using System.Globalization;
using Xunit;
using static MoneyRecord.Tests.TestCulture;

namespace MoneyRecord.Tests.Helpers;

/// <summary>
/// Formatting in an explicit currency (no test here depends on the app-wide base currency).
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
    public void FormatTotals_NeverAddsDifferentCurrencies()
    {
        WithCulture("en-US", () =>
            MoneyFormatter.FormatTotals([(10000m, "JPY"), (12.5m, "USD"), (7.5m, "usd")])
                .Should().Be("¥10,000 + $20.00"));
    }

    [Fact]
    public void FormatTotals_OneCurrency_IsASingleAmount()
    {
        WithCulture("en-US", () =>
            MoneyFormatter.FormatTotals([(1m, "EUR"), (2m, "EUR")]).Should().Be("€3.00"));
    }

    [Fact]
    public void AccountGroup_UsesTheAccountsCurrency_EvenWithATransferFromAnotherCurrency()
    {
        var items = new List<MoneyRecord.Models.Transaction>
        {
            new() { CurrencyCode = "EUR", Type = MoneyRecord.Models.TransactionType.Expense },
            new() { CurrencyCode = "USD", Type = MoneyRecord.Models.TransactionType.Transfer } // incoming, source in USD
        };

        var group = new MoneyRecord.Models.TransactionGroup("Euros", items, MoneyRecord.Models.GroupingMode.Account, 500m, currencyCode: "EUR");

        group.CurrencyCode.Should().Be("EUR");
    }
}
