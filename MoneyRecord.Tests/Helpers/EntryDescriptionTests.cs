using AwesomeAssertions;
using MoneyRecord.Helpers;
using Xunit;

namespace MoneyRecord.Tests.Helpers;

public class EntryDescriptionTests
{
    [Theory]
    [InlineData("", "Food")]
    [InlineData("   ", "Food")]
    [InlineData(null, "Food")]
    [InlineData(" Lunch ", "Lunch")]
    public void An_empty_description_is_saved_as_the_category_name(string? description, string expected)
    {
        EntryDescription.Resolve(description, "Food").Should().Be(expected);
    }

    [Theory]
    [InlineData("Food", "Food", "")]
    [InlineData("Lunch", "Food", "Lunch")]
    [InlineData("Food", null, "Food")]
    public void An_automatic_description_is_empty_when_editing(string saved, string? category, string expected)
    {
        EntryDescription.ForEditing(saved, category).Should().Be(expected);
    }

    [Fact]
    public void An_edited_automatic_description_follows_a_category_change()
    {
        var shown = EntryDescription.ForEditing("Food", "Food");

        EntryDescription.Resolve(shown, "Transport").Should().Be("Transport");
    }
}
