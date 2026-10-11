using AwesomeAssertions;
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
    public void GetCurrencyCode_UnsupportedCurrency_FallsBackToUsd()
    {
        // Thai baht is not in the supported currency list
        CurrencyService.GetCurrencyCode(new CultureInfo("th-TH")).Should().Be("USD");
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
}
