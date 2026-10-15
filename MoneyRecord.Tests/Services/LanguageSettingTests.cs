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
    public void Background_threads_format_in_the_app_language_too()
    {
        // The daily reminders job runs on a WorkManager thread; the language that differs from the device's
        var language = LocalizationService.DeviceCulture.TwoLetterISOLanguageName == "es" ? "en" : "es";
        Sut.SetLanguage(language, _preferences);

        // A new thread that inherits nothing from this one, like a Java thread
        CultureInfo? culture = null;
        var thread = new Thread(() => culture = CultureInfo.CurrentCulture);
        using (ExecutionContext.SuppressFlow())
            thread.Start();
        thread.Join();

        culture!.TwoLetterISOLanguageName.Should().Be(language);
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
