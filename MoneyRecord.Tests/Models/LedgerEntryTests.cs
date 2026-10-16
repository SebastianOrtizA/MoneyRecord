using AwesomeAssertions;
using MoneyRecord.Models;
using Xunit;

namespace MoneyRecord.Tests.Models;

public class LedgerEntryTests
{
    private static LedgerEntry Entry(string description, string payee = "") => new()
    {
        Kind = LedgerEntryKind.Expense, SourceId = 1, Description = description, CategoryName = "Food",
        AccountName = "Cash", PayeeName = payee
    };

    [Fact]
    public void Without_a_payee_the_row_is_description_over_category_and_account()
    {
        var entry = Entry("Lunch");

        (entry.Title, entry.Details).Should().Be(("Lunch", "Food · Cash"));
    }

    [Fact]
    public void An_automatic_description_gives_way_to_the_payee()
    {
        var entry = Entry("Food", payee: "Corner Bakery");

        (entry.Title, entry.Details).Should().Be(("Corner Bakery", "Food · Cash"));
    }

    [Fact]
    public void An_own_description_stays_the_title_and_the_payee_leads_the_details()
    {
        var entry = Entry("Birthday cake", payee: "Corner Bakery");

        (entry.Title, entry.Details).Should().Be(("Birthday cake", "Corner Bakery · Food · Cash"));
    }

    [Fact]
    public void A_description_equal_to_the_payee_is_not_repeated()
    {
        var entry = Entry("Corner Bakery", payee: "Corner Bakery");

        (entry.Title, entry.Details).Should().Be(("Corner Bakery", "Food · Cash"));
    }
}
