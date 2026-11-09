using AwesomeAssertions;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using Xunit;

namespace MoneyRecord.Tests.Helpers;

public class TagReportBuilderTests
{
    private static Transaction Entry(string? tags, decimal amount, TransactionType type = TransactionType.Expense) =>
        new() { Type = type, Tags = tags, Amount = amount, Date = new DateTime(2026, 1, 5) };

    [Fact]
    public void Ranks_tags_by_amount_and_an_entry_with_two_tags_counts_under_each()
    {
        var report = TagReportBuilder.Build(
            [Entry("trip, food", 60), Entry("food", 20), Entry("gift", 20)], TransactionType.Expense);

        report.Total.Should().Be(100m);
        report.Tags.Select(t => (t.Name, t.Amount, t.Count)).Should()
            .Equal(("food", 80m, 2), ("trip", 60m, 1), ("gift", 20m, 1));
        report.Tags[0].Percent.Should().Be(80.0);
    }

    [Fact]
    public void Tags_differing_in_case_or_spacing_are_one_tag()
    {
        var report = TagReportBuilder.Build([Entry("Food", 10), Entry(" food ,FOOD", 5)], TransactionType.Expense);

        report.Tags.Should().ContainSingle().Which.Should().Match<TagAmount>(t => t.Name == "Food" && t.Amount == 15m && t.Count == 2);
    }

    [Fact]
    public void Untagged_entries_are_the_last_row_and_only_the_chosen_type_counts()
    {
        var report = TagReportBuilder.Build(
            [Entry("a", 10), Entry(null, 30), Entry("  ", 10), Entry("a", 500, TransactionType.Income)], TransactionType.Expense);

        report.Total.Should().Be(50m);
        report.Tags.Select(t => t.Name).Should().Equal("a", null);
        report.Tags[1].Should().Match<TagAmount>(t => t.Amount == 40m && t.Count == 2);
    }

    [Fact]
    public void Nothing_of_the_type_is_an_empty_report()
    {
        var report = TagReportBuilder.Build([Entry("a", 10, TransactionType.Income)], TransactionType.Expense);

        report.HasData.Should().BeFalse();
        report.Tags.Should().BeEmpty();
    }
}

public class TransactionTagsTests
{
    [Fact]
    public void Split_cleans_dedupes_and_keeps_order()
    {
        TransactionTags.Split(" b ,a,, B , new   year ").Should().Equal("b", "a", "new year");
        TransactionTags.Split(null).Should().BeEmpty();
    }

    [Fact]
    public void Replace_renames_merges_or_removes_a_tag_once()
    {
        TransactionTags.Replace("a, b", "A", "c").Should().Be("c, b");
        TransactionTags.Replace("a, b", "a", "B").Should().Be("B");
        TransactionTags.Replace("a, b", "a", null).Should().Be("b");
        TransactionTags.Replace("a", "a", null).Should().BeNull();
    }

    [Fact]
    public void Normalize_drops_commas_and_extra_spaces()
    {
        TransactionTags.Normalize("  road,  trip ").Should().Be("road trip");
        TransactionTags.Normalize(" , ").Should().BeEmpty();
    }
}
