using AwesomeAssertions;
using MoneyRecord.Helpers;
using System.Globalization;
using Xunit;

namespace MoneyRecord.Tests.Helpers;

public sealed class WhatsNewChangelogTests
{
    private static string FastlaneDir()
    {
        var root = AppContext.BaseDirectory;
        while (root != null && !File.Exists(Path.Combine(root, "MoneyRecord.slnx")))
            root = Path.GetDirectoryName(root);
        root.Should().NotBeNull("the repository root holds the fastlane folder");
        return Path.Combine(root!, "fastlane", "metadata", "android");
    }

    [Theory]
    [InlineData("en-US", "en-US")]
    [InlineData("en-GB", "en-US")]
    [InlineData("fr-FR", "en-US")]
    [InlineData("es-ES", "es-ES")]
    [InlineData("es-CO", "es-419")]
    [InlineData("es-MX", "es-419")]
    [InlineData("es-AR", "es-419")]
    [InlineData("es-419", "es-419")]
    [InlineData("es-US", "es-419")]
    [InlineData("es", "es-ES")]
    [InlineData("", "en-US")]
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
        var path = Path.Combine(FastlaneDir(), locale, "changelogs", "default.txt");
        File.Exists(path).Should().BeTrue();
        var text = File.ReadAllText(path).Trim();
        text.Should().NotBeEmpty();
        text.Length.Should().BeLessThanOrEqualTo(500, "Google Play cuts release notes at 500 characters");
    }

    private const string History = "## 0.16.0\n- Pockets\n- Debts\n\n## 0.13.1\n- Separators\n\n## 0.13.0\n- Parties\n";

    [Fact]
    public void An_update_shows_every_version_since_the_last_one_seen()
    {
        WhatsNewChangelog.Since(History, "0.13.0", "0.16.0")
            .Should().Be("0.16.0\n- Pockets\n- Debts\n\n0.13.1\n- Separators");
    }

    [Fact]
    public void One_new_version_shows_its_lines_without_a_heading()
    {
        WhatsNewChangelog.Since(History, "0.13.1", "0.16.0").Should().Be("- Pockets\n- Debts");
        WhatsNewChangelog.Since(History, "0.13.0", "0.13.1").Should().Be("- Separators");
    }

    [Fact]
    public void Notes_older_than_the_running_version_keep_their_heading()
    {
        // A release that forgot its history section must not pass the old notes off as its own
        WhatsNewChangelog.Since(History, "0.13.1", "0.17.0").Should().Be("0.16.0\n- Pockets\n- Debts");
    }

    [Fact]
    public void With_no_version_seen_only_the_newest_shows()
    {
        WhatsNewChangelog.Since(History, "", "0.16.0").Should().Be("- Pockets\n- Debts");
        WhatsNewChangelog.Since(History, "", "0.14.0").Should().Be("0.13.1\n- Separators");
    }

    [Fact]
    public void Nothing_shows_when_no_version_is_newer_than_the_last_seen_one()
    {
        WhatsNewChangelog.Since(History, "0.16.0-rc1", "0.16.0").Should().BeNull();
        WhatsNewChangelog.Since(History, "0.16.0", "0.16.0").Should().BeNull();
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("es-ES")]
    [InlineData("es-419")]
    public void Every_locale_has_a_history_listing_the_same_versions(string locale)
    {
        var dir = FastlaneDir();

        string HistoryPath(string l) => Path.Combine(dir, l, "changelogs", "history.txt");
        string[] Versions(string l) => File.ReadAllLines(HistoryPath(l)).Where(x => x.StartsWith("## ")).ToArray();

        Versions(locale).Should().NotBeEmpty().And.Equal(Versions("en-US"));
        var newest = Versions(locale)[0][3..];
        WhatsNewChangelog.Since(File.ReadAllText(HistoryPath(locale)), "", newest).Should().StartWith("- ");
    }
}
