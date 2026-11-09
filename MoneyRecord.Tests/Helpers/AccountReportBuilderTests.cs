using AwesomeAssertions;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using Xunit;

namespace MoneyRecord.Tests.Helpers;

public class AccountReportBuilderTests
{
    private static Account Acc(int id, string name, string currency = "") => new() { Id = id, Name = name, CurrencyCode = currency };

    private static AccountTotal Total(int id, long cents) => new() { AccountId = id, Cents = cents };

    [Fact]
    public void Net_is_income_minus_expense_plus_transfers_per_account()
    {
        var groups = AccountReportBuilder.Build([Acc(1, "Bank")], "EUR",
            [Total(1, 100_00)], [Total(1, 30_00)], [Total(1, 20_00)], [Total(1, 5_00)],
            new Dictionary<int, decimal> { [1] = 250m });

        var row = groups.Should().ContainSingle().Which.Rows.Should().ContainSingle().Which;
        (row.Income, row.Expense, row.Transfers, row.Net, row.Balance).Should().Be((100m, 30m, -15m, 55m, 250m));
    }

    [Fact]
    public void Currencies_are_separate_groups_base_first_and_never_added()
    {
        var groups = AccountReportBuilder.Build([Acc(1, "Cash", "USD"), Acc(2, "Bank"), Acc(3, "Aaa", "USD")], "EUR",
            [Total(1, 10_00), Total(2, 5_00)], [], [], [], new Dictionary<int, decimal>());

        groups.Select(g => g.CurrencyCode).Should().Equal("EUR", "USD");
        groups[1].Rows.Select(r => r.Name).Should().Equal("Aaa", "Cash");
        groups[1].Income.Should().Be(10m);
        groups[0].Income.Should().Be(5m);
    }

    [Fact]
    public void Accounts_without_activity_are_listed_with_zeros()
    {
        var row = AccountReportBuilder.Build([Acc(9, "Idle")], "EUR", [], [], [], [], new Dictionary<int, decimal>())[0].Rows[0];

        (row.Income, row.Expense, row.Net, row.Balance).Should().Be((0m, 0m, 0m, 0m));
    }
}
