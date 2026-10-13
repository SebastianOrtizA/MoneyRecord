using AwesomeAssertions;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using Xunit;

namespace MoneyRecord.Tests.Helpers;

public class MonthlyTotalsBuilderTests
{
    private static Transaction Tx(TransactionType type, DateTime date, decimal amount) =>
        new() { Type = type, Date = date, Amount = amount };

    [Fact]
    public void Window_covers_whole_calendar_months_up_to_this_one()
    {
        var (start, end) = MonthlyTotalsBuilder.Window(new DateTime(2026, 10, 13, 15, 30, 0), 3);

        start.Should().Be(new DateTime(2026, 8, 1));
        end.Should().Be(new DateTime(2026, 11, 1).AddTicks(-1));
    }

    [Fact]
    public void One_row_per_month_with_unsigned_income_and_expense_and_zeros_for_empty_months()
    {
        var rows = MonthlyTotalsBuilder.Build(
        [
            Tx(TransactionType.Expense, new DateTime(2026, 8, 31, 23, 0, 0), -40m),
            Tx(TransactionType.Expense, new DateTime(2026, 8, 2), 10m),
            Tx(TransactionType.Income, new DateTime(2026, 10, 1), 500m)
        ], new DateTime(2026, 8, 15), 3);

        rows.Should().Equal(
            new MonthTotal(new DateTime(2026, 8, 1), 0m, 50m),
            new MonthTotal(new DateTime(2026, 9, 1), 0m, 0m),
            new MonthTotal(new DateTime(2026, 10, 1), 500m, 0m));
    }
}
