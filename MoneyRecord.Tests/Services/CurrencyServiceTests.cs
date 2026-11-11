using AwesomeAssertions;
using NSubstitute;
using MoneyRecord.Services;
using System.Globalization;
using Xunit;

namespace MoneyRecord.Tests.Services;

public class CurrencyServiceTests
{
    [Theory]
    [InlineData("es-CO", "COP")]
    [InlineData("en-US", "USD")]
    [InlineData("fr-FR", "EUR")]
    [InlineData("de-DE", "EUR")]
    [InlineData("ja-JP", "JPY")]
    public void GetCurrencyCode_UsesTheCultureRegion(string culture, string expected)
    {
        CurrencyService.GetCurrencyCode(new CultureInfo(culture)).Should().Be(expected);
    }

    [Fact]
    public void GetCurrencyCode_NeutralOrInvariantCulture_FallsBackToUsd()
    {
        CurrencyService.GetCurrencyCode(new CultureInfo("es")).Should().Be("USD");
        CurrencyService.GetCurrencyCode(CultureInfo.InvariantCulture).Should().Be("USD");
    }

    [Fact]
    public void GetCurrencyCode_CoversCurrenciesOutsideTheCommonOnes()
    {
        CurrencyService.GetCurrencyCode(new CultureInfo("th-TH")).Should().Be("THB");
    }

    [Fact]
    public void TheListIsSortedByCode_WithoutDuplicates()
    {
        var codes = new CurrencyService(Substitute.For<IPreferencesService>()).GetAvailableCurrencies().Select(c => c.Code).ToList();

        codes.Should().BeInAscendingOrder(StringComparer.Ordinal).And.OnlyHaveUniqueItems().And.Contain(["USD", "THB", "VND"]);
    }

    [Fact]
    public void GetDeviceCurrencyCode_IgnoresTheAppLanguage()
    {
        var expected = CurrencyService.GetCurrencyCode(LocalizationService.DeviceCulture);
        var original = CultureInfo.CurrentCulture;
        try
        {
            // The app language overrides CurrentCulture; the device currency must not follow it
            CultureInfo.CurrentCulture = new CultureInfo("ja-JP");
            CurrencyService.GetDeviceCurrencyCode().Should().Be(expected);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Theory]
    [InlineData("KES", "KSh")]
    [InlineData("UGX", "USh")]
    [InlineData("EGP", "E£")]
    [InlineData("AED", "AED")]
    public void A_currency_shows_its_local_symbol_or_else_its_code(string code, string symbol)
    {
        CurrencyService.GetSymbol(code).Should().Be(symbol);
    }

    [Theory]
    [InlineData("GBP")]
    [InlineData("EUR")]
    [InlineData("INR")]
    [InlineData("KES")]
    [InlineData("BRL")]
    public void A_major_currency_symbol_is_not_taken_by_a_minor_one(string code)
    {
        // A shared symbol makes the app add the code after every amount outside the base currency
        CurrencyService.HasSharedSymbol(code).Should().BeFalse();
    }
}
