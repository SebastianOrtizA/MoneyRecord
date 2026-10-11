using AwesomeAssertions;
using MoneyRecord.Services;
using System.Collections;
using System.Globalization;
using System.Resources;
using System.Text.RegularExpressions;
using Xunit;

namespace MoneyRecord.Tests.Services;

/// <summary>Every English string has a Spanish translation with the same format placeholders, and vice versa.</summary>
public partial class ResourceParityTests
{
    private static readonly ResourceManager Resources =
        new("MoneyRecord.Resources.Strings.AppResources", typeof(LocalizationService).Assembly);

    private static Dictionary<string, string> Load(CultureInfo culture)
    {
        // tryParents: false, so a missing Spanish key isn't filled in from English
        var set = Resources.GetResourceSet(culture, createIfNotExists: true, tryParents: false)
                  ?? throw new InvalidOperationException($"No resources for '{culture.Name}'.");
        return set.Cast<DictionaryEntry>().ToDictionary(e => (string)e.Key, e => e.Value as string ?? string.Empty);
    }

    private static readonly Dictionary<string, string> English = Load(CultureInfo.InvariantCulture);
    private static readonly Dictionary<string, string> Spanish = Load(new CultureInfo("es"));

    [Fact]
    public void SpanishHasEveryEnglishKey()
    {
        English.Keys.Except(Spanish.Keys).Should().BeEmpty();
    }

    [Fact]
    public void SpanishHasNoExtraKeys()
    {
        Spanish.Keys.Except(English.Keys).Should().BeEmpty();
    }

    [Fact]
    public void TranslationsAreNotEmpty()
    {
        Spanish.Where(e => string.IsNullOrWhiteSpace(e.Value)).Select(e => e.Key).Should().BeEmpty();
    }

    [Fact]
    public void TranslationsUseTheSamePlaceholders()
    {
        var mismatched = English.Keys.Intersect(Spanish.Keys)
            .Where(key => !Placeholders(English[key]).SetEquals(Placeholders(Spanish[key])))
            .ToList();

        mismatched.Should().BeEmpty();
    }

    private static HashSet<string> Placeholders(string text) =>
        PlaceholderRegex().Matches(text).Select(m => m.Groups[1].Value).ToHashSet();

    // {0}, {1:N2}, {2,10} → "0", "1", "2"
    [GeneratedRegex(@"\{(\d+)(?:[,:][^}]*)?\}")]
    private static partial Regex PlaceholderRegex();
}
