using AwesomeAssertions;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using Xunit;

namespace MoneyRecord.Tests.Helpers;

public class PeriodCompareBuilderTests
{
    private static Transaction Entry(int category, string name, decimal amount, TransactionType type = TransactionType.Expense) =>
        new() { Type = type, CategoryId = category, CategoryName = name, Amount = amount, Date = new DateTime(2026, 1, 5) };

    [Fact]
    public void Rows_hold_both_totals_and_a_category_in_one_period_has_zero_in_the_other()
    {
        var result = PeriodCompareBuilder.Build([Entry(1, "Food", 30), Entry(1, "Food", 20), Entry(2, "Fun", 10)],
            [Entry(1, "Food", 40), Entry(3, "Gym", 25)], TransactionType.Expense, "?");

        result.Rows.Select(r => (r.CategoryId, r.AmountA, r.AmountB)).Should()
            .Equal((1, 50m, 40m), (3, 0m, 25m), (2, 10m, 0m));
        (result.TotalA, result.TotalB).Should().Be((60m, 65m));
    }

    [Fact]
    public void Delta_is_a_against_b_and_nothing_before_has_no_percent()
    {
        var result = PeriodCompareBuilder.Build([Entry(1, "Food", 150), Entry(2, "Fun", 5)], [Entry(1, "Food", 100)],
            TransactionType.Expense, "?");

        result.Rows[0].Delta.Change.Should().Be(0.5m);
        result.Rows[1].Delta.Change.Should().BeNull();
    }

    [Fact]
    public void Only_the_asked_type_counts_and_empty_periods_have_no_data()
    {
        PeriodCompareBuilder.Build([Entry(1, "Pay", 900, TransactionType.Income)], [], TransactionType.Expense, "?")
            .HasData.Should().BeFalse();
    }

    [Fact]
    public void Unnamed_categories_use_the_fallback_name()
    {
        PeriodCompareBuilder.Build([Entry(9, "", 5)], [], TransactionType.Expense, "Unknown").Rows[0].Name.Should().Be("Unknown");
    }

    [Fact]
    public void Presets_pair_the_later_period_with_the_earlier_one()
    {
        var today = new DateTime(2026, 3, 15, 14, 0, 0);

        var (a, b) = ComparePresets.Resolve(ComparePreset.ThisMonthVsLast, today);
        (a.Start, b.Start).Should().Be((new DateTime(2026, 3, 1), new DateTime(2026, 2, 1)));
        b.End.Should().Be(new DateTime(2026, 3, 1).AddTicks(-1));

        (a, b) = ComparePresets.Resolve(ComparePreset.ThisMonthVsLast, new DateTime(2026, 1, 10));
        b.Start.Should().Be(new DateTime(2025, 12, 1));

        (a, b) = ComparePresets.Resolve(ComparePreset.ThisYearVsLast, today);
        (a.Start, b.Start).Should().Be((new DateTime(2026, 1, 1), new DateTime(2025, 1, 1)));

        (a, b) = ComparePresets.Resolve(ComparePreset.SameMonthLastYear, today);
        (a.Start, b.Start, b.End).Should().Be((new DateTime(2026, 3, 1), new DateTime(2025, 3, 1), new DateTime(2025, 4, 1).AddTicks(-1)));

        (a, b) = ComparePresets.Resolve(ComparePreset.Last30Days, today);
        (a.Start, a.End).Should().Be((new DateTime(2026, 2, 14), new DateTime(2026, 3, 16).AddTicks(-1)));
        b.End.Should().Be(new DateTime(2026, 2, 14).AddTicks(-1));
        (b.End - b.Start).Should().Be(a.End - a.Start);
    }
}
