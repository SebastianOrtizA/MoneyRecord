using AwesomeAssertions;
using MoneyRecord.Models;
using MoneyRecord.Services;
using System.Collections;
using System.Globalization;
using System.Resources;
using System.Text.RegularExpressions;
using Xunit;

namespace MoneyRecord.Tests.Services;

/// <summary>Every English string has a translation in every language of <see cref="AppLanguage.All"/>, with the same format placeholders, and vice versa.</summary>
public partial class ResourceParityTests
{
    private static readonly ResourceManager Resources =
        new("MoneyRecord.Resources.Strings.AppResources", typeof(LocalizationService).Assembly);

    private static Dictionary<string, string> Load(CultureInfo culture)
    {
        // tryParents: false, so a missing translated key isn't filled in from English
        var set = Resources.GetResourceSet(culture, createIfNotExists: true, tryParents: false)
                  ?? throw new InvalidOperationException($"No resources for '{culture.Name}'.");
        return set.Cast<DictionaryEntry>().ToDictionary(e => (string)e.Key, e => e.Value as string ?? string.Empty);
    }

    private static readonly Dictionary<string, string> English = Load(CultureInfo.InvariantCulture);

    /// <summary>Every translated language's code (English is the neutral resource).</summary>
    public static IEnumerable<object[]> Languages =>
        AppLanguage.All.Where(l => l.Code != "en").Select(l => new object[] { l.Code });

    private static Dictionary<string, string> Translation(string code) => Load(new CultureInfo(code));

    [Theory]
    [MemberData(nameof(Languages))]
    public void EveryEnglishKeyIsTranslated(string code)
    {
        English.Keys.Except(Translation(code).Keys).Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void TranslationsHaveNoExtraKeys(string code)
    {
        Translation(code).Keys.Except(English.Keys).Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void TranslationsAreNotEmpty(string code)
    {
        Translation(code).Where(e => string.IsNullOrWhiteSpace(e.Value)).Select(e => e.Key).Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void TranslationsUseTheSamePlaceholders(string code)
    {
        var translation = Translation(code);
        var mismatched = English.Keys.Intersect(translation.Keys)
            .Where(key => !Placeholders(English[key]).SetEquals(Placeholders(translation[key])))
            .ToList();

        mismatched.Should().BeEmpty();
    }

    private static HashSet<string> Placeholders(string text) =>
        PlaceholderRegex().Matches(text).Select(m => m.Groups[1].Value).ToHashSet();

    // {0}, {1:N2}, {2,10} → "0", "1", "2"
    [GeneratedRegex(@"\{(\d+)(?:[,:][^}]*)?\}")]
    private static partial Regex PlaceholderRegex();
}
