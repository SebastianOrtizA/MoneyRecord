using AwesomeAssertions;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Services.Pdf;
using Xunit;

namespace MoneyRecord.Tests.Helpers;

[Collection("Language")]
public class YearReviewSectionsTests
{
    private static YearReview Review() => YearReviewBuilder.Build(2026,
    [
        new Transaction { Type = TransactionType.Income, Amount = 1000, Date = new DateTime(2026, 1, 2), CategoryId = 9, CategoryName = "Pay" },
        new Transaction { Type = TransactionType.Expense, Amount = 250, Date = new DateTime(2026, 2, 2), CategoryId = 1, CategoryName = "Food", Description = "Big shop" },
        new Transaction { Type = TransactionType.Expense, Amount = 50, Date = new DateTime(2026, 3, 2), CategoryId = 1, CategoryName = "Food" }
    ], [1], "EUR", false, "(none)", new DateTime(2027, 1, 1));

    [Fact]
    public void An_empty_year_has_no_sections()
    {
        YearReviewSections.Build(YearReviewBuilder.Build(2026, [], [], "EUR", false, "", new DateTime(2027, 1, 1)), true)
            .Should().BeEmpty();
    }

    [Fact]
    public void With_amounts_the_text_holds_money_and_without_it_holds_none()
    {
        var review = Review();
        var with = YearReviewSections.ToText(review, includeAmounts: true);
        var without = YearReviewSections.ToText(review, includeAmounts: false);

        with.Should().Contain(MoneyFormatter.Format(1000m, "EUR")).And.Contain(MoneyFormatter.Format(300m, "EUR"));
        without.Should().NotContain("1000").And.NotContain("250").And.NotContain("300");
        // Shares and the savings rate remain
        without.Should().Contain("100%").And.Contain("70%");
    }

    [Fact]
    public void Sections_without_amounts_drop_the_income_expense_and_net_lines()
    {
        var overview = YearReviewSections.Build(Review(), includeAmounts: false)[0];

        overview.Lines.Should().ContainSingle();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void The_pdf_renders(bool includeAmounts)
    {
        using var stream = new MemoryStream();

        YearReviewPdf.Write(Review(), includeAmounts, stream);

        stream.Length.Should().BeGreaterThan(1000);
        stream.ToArray().Take(4).Should().Equal("%PDF"u8.ToArray());
    }
}
