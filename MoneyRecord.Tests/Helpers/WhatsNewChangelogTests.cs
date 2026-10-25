using AwesomeAssertions;
using MoneyRecord.Helpers;
using System.Globalization;
using Xunit;

namespace MoneyRecord.Tests.Helpers;

public sealed class WhatsNewChangelogTests
{
    [Theory]
    [InlineData("en-US", "en-US")]
    [InlineData("en-GB", "en-US")]
    [InlineData("fr-FR", "en-US")]
    [InlineData("es-ES", "es-ES")]
    [InlineData("es-CO", "es-419")]
    [InlineData("es-MX", "es-419")]
    [InlineData("es-AR", "es-419")]
    [InlineData("es", "es-ES")]
    public void A_culture_gets_the_store_text_that_suits_it(string culture, string expected)
    {
        WhatsNewChangelog.Locale(new CultureInfo(culture)).Should().Be(expected);
    }

    [Fact]
    public void The_asset_path_is_the_locale_in_the_changelog_folder()
    {
        WhatsNewChangelog.AssetPath("es-419").Should().Be("Changelog/es-419.txt");
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("es-ES")]
    [InlineData("es-419")]
    public void Every_locale_the_app_picks_has_a_store_changelog_that_fits_the_play_limit(string locale)
    {
        var root = AppContext.BaseDirectory;
        while (root != null && !File.Exists(Path.Combine(root, "MoneyRecord.slnx")))
            root = Path.GetDirectoryName(root);
        root.Should().NotBeNull("the repository root holds the fastlane folder");

        var path = Path.Combine(root!, "fastlane", "metadata", "android", locale, "changelogs", "default.txt");
        File.Exists(path).Should().BeTrue();
        var text = File.ReadAllText(path).Trim();
        text.Should().NotBeEmpty();
        text.Length.Should().BeLessThanOrEqualTo(500, "Google Play cuts release notes at 500 characters");
    }
}
