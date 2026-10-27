using AwesomeAssertions;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Services;
using System.Globalization;
using Xunit;

namespace MoneyRecord.Tests.Services;

/// <summary>A row in <see cref="AppLanguage.All"/> is only complete with its widget strings, store listing and pop-up notes.</summary>
public class LanguageCoverageTests
{
    private static string Root()
    {
        var root = AppContext.BaseDirectory;
        while (root != null && !File.Exists(Path.Combine(root, "MoneyRecord.slnx")))
            root = Path.GetDirectoryName(root);
        root.Should().NotBeNull("the repository root holds the fastlane folder");
        return root!;
    }

    public static IEnumerable<object[]> Languages => AppLanguage.All.Select(l => new object[] { l.Code });

    // es-419 has no AppLanguage row (Latin America shares "es"), but it is a store locale of its own
    public static IEnumerable<object[]> StoreLocales =>
        new[] { "en-US", "es-ES", "es-419" }
            .Concat(AppLanguage.All.Where(l => l.Code is not ("en" or "es")).Select(l => l.DefaultCulture))
            .Select(l => new object[] { l });

    [Theory]
    [MemberData(nameof(Languages))]
    public void Every_default_culture_is_a_specific_culture_of_its_language(string code)
    {
        var language = AppLanguage.Find(code)!;
        var culture = new CultureInfo(language.DefaultCulture);
        culture.IsNeutralCulture.Should().BeFalse();
        culture.TwoLetterISOLanguageName.Should().Be(code);
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void Every_language_has_widget_strings(string code)
    {
        if (code == "en")
            return; // the default values/ folder
        File.Exists(Path.Combine(Root(), "MoneyRecord", "Platforms", "Android", "Resources", $"values-{code}", "strings.xml"))
            .Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(StoreLocales))]
    public void Every_store_locale_has_a_listing_that_fits_Play(string locale)
    {
        var dir = Path.Combine(Root(), "fastlane", "metadata", "android", locale);
        string Read(string file) => File.ReadAllText(Path.Combine(dir, file)).Trim();

        Read("title.txt").Length.Should().BeInRange(1, 30);
        Read("short_description.txt").Length.Should().BeInRange(1, 80);
        Read("full_description.txt").Length.Should().BeInRange(1, 4000);
    }

    [Theory]
    [MemberData(nameof(StoreLocales))]
    public void Every_store_locale_is_bundled_as_a_pop_up_asset(string locale)
    {
        var project = File.ReadAllText(Path.Combine(Root(), "MoneyRecord", "MoneyRecord.csproj"));
        project.Should().Contain($"LogicalName=\"{WhatsNewChangelog.AssetPath(locale)}\"");
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void Every_language_gets_a_store_locale_that_has_a_folder(string code)
    {
        var language = AppLanguage.Find(code)!;
        var locale = WhatsNewChangelog.Locale(new CultureInfo(language.DefaultCulture));
        Directory.Exists(Path.Combine(Root(), "fastlane", "metadata", "android", locale)).Should().BeTrue();
    }
}
