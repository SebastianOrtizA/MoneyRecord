using AwesomeAssertions;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using Xunit;

namespace MoneyRecord.Tests.Helpers;

public class CategoryReportBuilderTests
{
    private static Transaction Tx(TransactionType type, int categoryId, string category, decimal amount) => new()
    {
        Type = type, CategoryId = categoryId, CategoryName = category, CategoryIconCode = "F0001", Amount = amount
    };

    [Fact]
    public void Only_the_reports_type_counts_and_shares_add_up()
    {
        var report = CategoryReportBuilder.Build(
        [
            Tx(TransactionType.Expense, 1, "Rent", 75m),
            Tx(TransactionType.Expense, 2, "Food", 20m),
            Tx(TransactionType.Expense, 2, "Food", 5m),
            Tx(TransactionType.Income, 3, "Salary", 1000m)
        ], CategoryType.Expense);

        report.Total.Should().Be(100m);
        report.Categories.Select(c => c.CategoryName).Should().Equal("Rent", "Food");
        report.Categories[1].Amount.Should().Be(25m);
        report.Categories[1].Percentage.Should().Be(25);
        report.Categories.Should().AllSatisfy(c => c.Type.Should().Be(CategoryType.Expense));
    }

    [Fact]
    public void The_largest_share_is_the_hottest_level_and_the_smallest_the_coolest()
    {
        var report = CategoryReportBuilder.Build(
        [
            Tx(TransactionType.Income, 1, "Salary", 90m),
            Tx(TransactionType.Income, 2, "Gifts", 5m),
            Tx(TransactionType.Income, 3, "Interest", 5m)
        ], CategoryType.Income);

        report.Categories[0].HeatLevel.Should().Be(CategoryAmount.HeatLevels - 1);
        report.Categories[1].HeatLevel.Should().Be(0);
        report.Categories[1].IsMinorShare.Should().BeTrue();
        report.Categories[0].IsMinorShare.Should().BeFalse();
        report.Categories[0].IsIncome.Should().BeTrue();
    }

    [Fact]
    public void Equal_shares_sit_in_the_middle()
    {
        CategoryReportBuilder.HeatLevel(50, 50, 50).Should().Be(5);
    }

    [Fact]
    public void Same_named_categories_stay_apart_and_amounts_are_unsigned()
    {
        var report = CategoryReportBuilder.Build(
        [
            Tx(TransactionType.Expense, 1, "Misc", -30m),
            Tx(TransactionType.Expense, 2, "Misc", 10m)
        ], CategoryType.Expense);

        report.Total.Should().Be(40m);
        report.Categories.Should().HaveCount(2);
    }

    [Fact]
    public void No_matching_transactions_is_an_empty_report()
    {
        var report = CategoryReportBuilder.Build([Tx(TransactionType.Income, 1, "Salary", 10m)], CategoryType.Expense);

        report.Total.Should().Be(0);
        report.Categories.Should().BeEmpty();
    }
}
