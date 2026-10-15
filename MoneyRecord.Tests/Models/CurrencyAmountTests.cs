using AwesomeAssertions;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using Xunit;

namespace MoneyRecord.Tests.Models;

public class CurrencyAmountTests
{
    [Fact]
    public void Totals_sum_per_currency_base_first_and_drop_zeros()
    {
        var totals = CurrencyAmount.Totals(
        [
            new("USD", 5m), new("", 10m), new("EUR", 1m), new("usd", 2m), new("EUR", -1m), new("COP", 3m)
        ], "COP");

        totals.Should().Equal(new CurrencyAmount("COP", 13m), new CurrencyAmount("USD", 7m));
    }

    [Fact]
    public void Period_totals_have_one_row_per_currency_and_always_the_base_first()
    {
        var rows = PeriodTotals.Build([new("COP", 100m), new("USD", 10m)], [new("COP", 30m), new("EUR", 4m)], "JPY");

        rows.Select(r => (r.CurrencyCode, r.Income, r.Expense, r.Net)).Should().Equal(
            ("JPY", 0m, 0m, 0m), ("COP", 100m, 30m, 70m), ("EUR", 0m, 4m, -4m), ("USD", 10m, 0m, 10m));
        rows[2].IsNetNegative.Should().BeTrue();
        rows[1].NetText.Should().Be(MoneyFormatter.FormatSigned(70m, "COP"));
    }

    [Fact]
    public void Format_lists_each_currency_and_is_a_zero_when_empty()
    {
        CurrencyAmount.Format([new("EUR", 2m), new("USD", -1m)])
            .Should().Be($"{MoneyFormatter.Format(2m, "EUR")} · {MoneyFormatter.Format(-1m, "USD")}");
        CurrencyAmount.Format([]).Should().Be(MoneyFormatter.Format(0));
    }
}
