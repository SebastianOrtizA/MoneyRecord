using AwesomeAssertions;
using MoneyRecord.Helpers;
using Xunit;

namespace MoneyRecord.Tests.Helpers;

public class ListSearchTests
{
    [Theory]
    [InlineData("", true)]
    [InlineData(null, true)]
    [InlineData("   ", true)]
    [InlineData("bak", true)]
    [InlineData("CAFE", true)]
    [InlineData("café", true)]
    [InlineData("  bakery   &  ", true)]
    [InlineData("pharmacy", false)]
    public void Matches_finds_the_text_anywhere_ignoring_case_and_accents(string? query, bool expected)
    {
        ListSearch.Matches("Bakery & Café", query).Should().Be(expected);
    }

    [Fact]
    public void Nothing_in_a_missing_text_matches_a_typed_query()
    {
        ListSearch.Matches(null, "a").Should().BeFalse();
        ListSearch.Matches(null, "").Should().BeTrue();
    }

    [Theory]
    [InlineData(5, "ab", "")]
    [InlineData(8, "ab", "")]
    [InlineData(9, "ab", "ab")]
    [InlineData(9, null, "")]
    public void A_list_too_short_for_the_search_box_ignores_what_was_typed(int count, string? typed, string expected)
    {
        ListSearch.EffectiveQuery(count, typed).Should().Be(expected);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(8, false)]
    [InlineData(9, true)]
    public void A_list_shows_its_search_box_from_nine_rows(int count, bool expected)
    {
        ListSearch.ShowsSearch(count).Should().Be(expected);
    }
}
