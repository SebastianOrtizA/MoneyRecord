using AwesomeAssertions;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using Xunit;

namespace MoneyRecord.Tests.Helpers;

public class CategoryReportBuilderTests
{
    private static Transaction Tx(TransactionType type, int categoryId, string category, decimal amount) => new()
    {
        Type = type, CategoryId = categoryId, CategoryName = category, CategoryIconCode = "F0001", Amount = amount
    };

    private static readonly int[] Ids = [1, 2, 3];

    [Fact]
    public void Only_the_reports_type_counts_and_shares_add_up()
    {
        var report = CategoryReportBuilder.Build(
        [
            Tx(TransactionType.Expense, 1, "Rent", 75m),
            Tx(TransactionType.Expense, 2, "Food", 20m),
            Tx(TransactionType.Expense, 2, "Food", 5m),
            Tx(TransactionType.Income, 3, "Salary", 1000m)
        ], CategoryType.Expense, Ids);

        report.Total.Should().Be(100m);
        report.Categories.Select(c => c.CategoryName).Should().Equal("Rent", "Food");
        report.Categories[1].Amount.Should().Be(25m);
        report.Categories[1].Percentage.Should().Be(25);
        report.Categories.Should().AllSatisfy(c => c.Type.Should().Be(CategoryType.Expense));
    }

    [Fact]
    public void Colour_slot_follows_the_category_not_its_rank()
    {
        // Category 3 is the largest share but keeps slot 2: its place among the IDs
        var report = CategoryReportBuilder.Build(
        [
            Tx(TransactionType.Income, 3, "Salary", 90m),
            Tx(TransactionType.Income, 1, "Gifts", 10m)
        ], CategoryType.Income, [3, 1, 2]);

        report.Categories.Select(c => (c.CategoryId, c.PaletteSlot)).Should().Equal((3, 2), (1, 0));
        report.Categories[0].IsIncome.Should().BeTrue();
        report.Categories[0].Share.Should().Be(0.9);
    }

    [Fact]
    public void The_largest_categories_get_a_colour_even_with_the_highest_ID_and_only_the_small_rest_folds()
    {
        // Ten categories; the newest (highest ID) is the largest, two small ones are past the palette
        var ids = Enumerable.Range(1, 10).ToArray();
        var report = CategoryReportBuilder.Build(
            ids.Select(id => Tx(TransactionType.Expense, id, $"C{id}", id == 10 ? 500m : 100m - id)).ToList(),
            CategoryType.Expense, ids);

        report.Categories[0].CategoryId.Should().Be(10);
        report.Categories[0].PaletteSlot.Should().NotBe(CategoryAmount.OtherSlot);
        report.Categories.Where(c => c.CategoryId is 8 or 9).Should().AllSatisfy(c => c.PaletteSlot.Should().Be(CategoryAmount.OtherSlot));
        report.Categories.Where(c => c.PaletteSlot != CategoryAmount.OtherSlot).Select(c => c.PaletteSlot)
            .Should().OnlyHaveUniqueItems().And.HaveCount(CategoryAmount.PaletteSize);
        report.Slices.Should().HaveCount(CategoryAmount.PaletteSize + 1);
        report.Slices[^1].Should().Be(new ChartSlice(AppResources.OtherCategories, 92m + 91m, CategoryAmount.OtherSlot));
    }

    [Fact]
    public void A_deleted_category_still_gets_a_colour_when_it_is_large()
    {
        var report = CategoryReportBuilder.Build(
        [
            Tx(TransactionType.Expense, 1, "Rent", 10m),
            Tx(TransactionType.Expense, 99, "Deleted", 50m)
        ], CategoryType.Expense, Ids);

        report.Categories.Select(c => c.PaletteSlot).Should().OnlyHaveUniqueItems().And.NotContain(CategoryAmount.OtherSlot);
        report.Slices.Should().HaveCount(2);
    }

    [Fact]
    public void No_Other_slice_when_every_category_has_a_colour()
    {
        var report = CategoryReportBuilder.Build([Tx(TransactionType.Expense, 1, "Rent", 75m)], CategoryType.Expense, Ids);

        report.Slices.Should().ContainSingle().Which.PaletteSlot.Should().Be(0);
    }

    [Fact]
    public void Same_named_categories_stay_apart_and_amounts_are_unsigned()
    {
        var report = CategoryReportBuilder.Build(
        [
            Tx(TransactionType.Expense, 1, "Misc", -30m),
            Tx(TransactionType.Expense, 2, "Misc", 10m)
        ], CategoryType.Expense, Ids);

        report.Total.Should().Be(40m);
        report.Categories.Should().HaveCount(2);
    }

    [Fact]
    public void No_matching_transactions_is_an_empty_report()
    {
        var report = CategoryReportBuilder.Build([Tx(TransactionType.Income, 1, "Salary", 10m)], CategoryType.Expense, Ids);

        report.Total.Should().Be(0);
        report.Categories.Should().BeEmpty();
    }
}
