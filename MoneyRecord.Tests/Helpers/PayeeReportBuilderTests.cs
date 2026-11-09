using AwesomeAssertions;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using Xunit;

namespace MoneyRecord.Tests.Helpers;

public class PayeeReportBuilderTests
{
    private static Transaction Entry(int? payee, string name, decimal amount, TransactionType type = TransactionType.Expense) =>
        new() { Type = type, PayeeId = payee, PayeeName = name, Amount = amount, Date = new DateTime(2026, 1, 5) };

    [Fact]
    public void Ranks_payees_by_amount_with_count_average_and_share()
    {
        var report = PayeeReportBuilder.Build(
            [Entry(1, "Bakery", 10), Entry(2, "Shop", 60), Entry(1, "Bakery", 30)], TransactionType.Expense, "(none)");

        report.Total.Should().Be(100m);
        report.Payees.Select(p => p.Name).Should().Equal("Shop", "Bakery");
        var bakery = report.Payees[1];
        (bakery.Amount, bakery.Count, bakery.AverageTicket, bakery.Percent).Should().Be((40m, 2, 20m, 40.0));
    }

    [Fact]
    public void Entries_without_a_payee_are_one_last_row()
    {
        var report = PayeeReportBuilder.Build([Entry(null, "", 500), Entry(3, "Shop", 5)], TransactionType.Expense, "(none)");

        report.Payees.Select(p => p.Name).Should().Equal("Shop", "(none)");
        report.Payees[1].PayeeId.Should().BeNull();
    }

    [Fact]
    public void Other_types_are_ignored_and_nothing_is_empty()
    {
        PayeeReportBuilder.Build([Entry(1, "Boss", 900, TransactionType.Income)], TransactionType.Expense, "(none)")
            .HasData.Should().BeFalse();
    }
}
