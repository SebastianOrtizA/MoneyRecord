using FluentAssertions;
using MoneyRecord.Services;
using System.Globalization;
using Xunit;

namespace MoneyRecord.Tests.Services;

public class LocalizationServiceTests
{
    [Theory]
    [InlineData("es", "es-CO", "es-CO")]
    [InlineData("es", "es-MX", "es-MX")]
    [InlineData("en", "en-GB", "en-GB")]
    public void ResolveCulture_SameLanguage_KeepsDeviceRegion(string language, string device, string expected)
    {
        LocalizationService.ResolveCulture(language, new CultureInfo(device)).Name.Should().Be(expected);
    }

    [Theory]
    [InlineData("en", "fr-FR", "en-US")]
    [InlineData("es", "en-US", "es-ES")]
    [InlineData("es", "es", "es-ES")]
    public void ResolveCulture_OtherLanguageOrNoRegion_FallsBack(string language, string device, string expected)
    {
        LocalizationService.ResolveCulture(language, new CultureInfo(device)).Name.Should().Be(expected);
    }

    [Fact]
    public void ResolveCulture_SpecificCode_IsUsedAsIs()
    {
        LocalizationService.ResolveCulture("es-AR", new CultureInfo("en-US")).Name.Should().Be("es-AR");
    }

    [Fact]
    public void ResolveCulture_NeverReturnsNeutralCulture()
    {
        LocalizationService.ResolveCulture("es", new CultureInfo("es-CO")).IsNeutralCulture.Should().BeFalse();
        LocalizationService.ResolveCulture("en", CultureInfo.InvariantCulture).IsNeutralCulture.Should().BeFalse();
    }
}
