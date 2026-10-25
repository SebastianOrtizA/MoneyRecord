using AwesomeAssertions;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using Xunit;

namespace MoneyRecord.Tests.Models;

public class DebtSummaryTotalsTests
{
    private static DebtSummary Make(DebtDirection direction, string currency, long total, long paid) =>
        new(new Debt { Direction = direction }, "Ana", currency, total, paid, 0, null, new DateTime(2026, 10, 25));

    [Fact]
    public void Totals_are_per_direction_and_currency_and_skip_paid_off_debts()
    {
        var debts = new[]
        {
            Make(DebtDirection.Lent, "USD", 10_000, 2_500),
            Make(DebtDirection.Lent, "USD", 5_000, 0),
            Make(DebtDirection.Lent, "EUR", 4_000, 4_000),
            Make(DebtDirection.Borrowed, "EUR", 3_000, 0)
        };

        DebtSummary.OutstandingTotals(debts, DebtDirection.Lent).Should().Be(MoneyFormatter.Format(125m, "USD"));
        DebtSummary.OutstandingTotals(debts, DebtDirection.Borrowed).Should().Be(MoneyFormatter.Format(30m, "EUR"));
    }

    [Fact]
    public void Nothing_open_gives_an_empty_text()
    {
        DebtSummary.OutstandingTotals([], DebtDirection.Lent).Should().BeEmpty();
    }

    [Fact]
    public void Detail_text_adds_the_due_date_only_when_there_is_one()
    {
        var open = Make(DebtDirection.Lent, "USD", 100, 0);
        open.DetailText.Should().Be(open.DirectionText);

        var due = new DebtSummary(new Debt { Direction = DebtDirection.Lent, DueDate = new DateTime(2026, 11, 30) }, "Ana", "USD", 100, 0, 0, null, new DateTime(2026, 10, 25));
        due.DetailText.Should().Be($"{due.DirectionText} · {due.DueText}").And.Contain("·");
    }
}
