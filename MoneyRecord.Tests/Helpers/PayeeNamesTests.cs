using AwesomeAssertions;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using Xunit;

namespace MoneyRecord.Tests.Helpers;

public class PayeeNamesTests
{
    private static PayeeSummary Payee(string name, int count = 1) => new(name.GetHashCode(), name, count, null);

    [Theory]
    [InlineData("  Corner   Bakery ", "Corner Bakery")]
    [InlineData("\tÉxito\n", "Éxito")]
    [InlineData("   ", "")]
    [InlineData(null, "")]
    public void Normalize_trims_and_collapses_spaces(string? typed, string expected)
    {
        PayeeNames.Normalize(typed).Should().Be(expected);
    }

    [Fact]
    public void Same_ignores_case_and_spacing_but_not_accents()
    {
        PayeeNames.Same("corner  bakery", "Corner Bakery").Should().BeTrue();
        PayeeNames.Same("éxito", "ÉXITO").Should().BeTrue();
        PayeeNames.Same("Exito", "Éxito").Should().BeFalse("two different names, which the user can merge");
    }

    [Fact]
    public void Suggest_ranks_name_prefix_then_word_prefix_then_contains_most_used_first()
    {
        var payees = new[]
        {
            Payee("Supermarket"), Payee("Superb Café", 9), Payee("The Super Store"), Payee("Ensuper"), Payee("Bakery", 50)
        };

        PayeeNames.Suggest(payees, "super").Select(p => p.Name)
            .Should().Equal("Superb Café", "Supermarket", "The Super Store", "Ensuper");
    }

    [Fact]
    public void Suggest_ignores_accents_and_stops_at_the_limit()
    {
        var payees = Enumerable.Range(1, 8).Select(i => Payee($"Café {i}", i)).ToList();

        PayeeNames.Suggest(payees, "cafe", max: 3).Select(p => p.Name).Should().Equal("Café 8", "Café 7", "Café 6");
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("corner bakery")] // typed in full: nothing left to suggest
    public void Suggest_offers_nothing_for_empty_or_complete_names(string typed)
    {
        PayeeNames.Suggest([Payee("Corner Bakery"), Payee("Corner Shop")], typed).Should().BeEmpty();
    }

    [Theory]
    [InlineData("  corner   BAKERY ", "Corner Bakery")]
    [InlineData("Corner", null)]
    [InlineData(" ", null)]
    public void Find_returns_the_payee_typed_in_full(string typed, string? expected)
    {
        (PayeeNames.Find([Payee("Corner Bakery"), Payee("Corner Shop")], typed)?.Name).Should().Be(expected);
    }

    [Fact]
    public void Uses_text_adds_recurring_transactions_only_when_there_are_some()
    {
        new PayeeSummary(1, "Gym", 2, null).UsesText.Should().Be(Plurals.Transactions(2));
        new PayeeSummary(1, "Gym", 0, null, RecurringCount: 1).UsesText
            .Should().Be($"{Plurals.Transactions(0)}, {Plurals.Recurring(1)}", "a payee only a schedule uses is not unused");
    }
}
