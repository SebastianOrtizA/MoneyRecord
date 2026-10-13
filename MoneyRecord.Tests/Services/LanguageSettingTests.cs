using System.Globalization;
using AwesomeAssertions;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services;
using Xunit;

namespace MoneyRecord.Tests.Services;

[Collection(LanguageCollection.Name)]
public sealed class LanguageSettingTests : LanguageTestBase
{
    private readonly FakePreferences _preferences = new();
    private static LocalizationService Sut => LocalizationService.Instance;

    [Fact]
    public void SetLanguage_saves_and_applies_it()
    {
        Sut.SetLanguage("es", _preferences);

        _preferences.Get(LocalizationService.LanguagePreferenceKey, string.Empty).Should().Be("es");
        Sut.LanguageSetting.Should().Be("es");
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Should().Be("es");
        AppResources.Culture!.TwoLetterISOLanguageName.Should().Be("es");
    }

    [Fact]
    public void LoadLanguage_applies_the_saved_language()
    {
        _preferences.Set(LocalizationService.LanguagePreferenceKey, "es");

        Sut.LoadLanguage(_preferences);

        Sut.LanguageSetting.Should().Be("es");
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Should().Be("es");
    }

    [Theory]
    [InlineData("")]
    [InlineData("fr")]
    public void An_empty_or_unsupported_language_follows_the_device(string saved)
    {
        _preferences.Set(LocalizationService.LanguagePreferenceKey, saved);

        Sut.LoadLanguage(_preferences);

        Sut.LanguageSetting.Should().BeEmpty();
        var deviceLanguage = LocalizationService.DeviceCulture.TwoLetterISOLanguageName == "es" ? "es" : "en";
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Should().Be(deviceLanguage);
    }

    [Theory]
    [InlineData("en", "English")]
    [InlineData("es", "Español")]
    public void Language_names_are_written_in_their_own_language(string code, string name)
    {
        LocalizationService.GetLanguageName(code).Should().Be(name);
    }
}
