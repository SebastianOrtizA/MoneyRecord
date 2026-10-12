using AwesomeAssertions;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using Xunit;

namespace MoneyRecord.Tests.Models;

public class CurrencyBalanceTests
{
    private static AccountBalanceInfo Balance(decimal amount, string currency) =>
        new() { CurrentBalance = amount, CurrencyCode = currency };

    [Fact]
    public void Never_adds_different_currencies_and_puts_the_base_first()
    {
        var result = CurrencyBalance.FromAccounts(
        [
            Balance(50m, "EUR"),
            Balance(1000m, "USD"),
            Balance(250m, ""),       // no currency = the base currency
            Balance(450000m, "COP"),
            Balance(200m, "eur")
        ], "USD");

        result.Should().Equal(
            new CurrencyBalance("USD", 1250m, 2),
            new CurrencyBalance("COP", 450000m, 1),
            new CurrencyBalance("EUR", 250m, 2));
    }

    [Fact]
    public void One_currency_is_one_row()
    {
        CurrencyBalance.FromAccounts([Balance(10m, "EUR"), Balance(5m, "EUR")], "USD")
            .Should().ContainSingle().Which.Should().Be(new CurrencyBalance("EUR", 15m, 2));
    }

    [Fact]
    public void One_currency_is_shown_without_its_code()
    {
        CurrencyBalance.DisplayLines([new CurrencyBalance("EUR", 15m, 2)])
            .Should().Equal(new BalanceLine(MoneyFormatter.Format(15m, "EUR"), "EUR"));
    }

    [Fact]
    public void Several_currencies_are_one_line_each_with_their_code()
    {
        CurrencyBalance.DisplayLines([new CurrencyBalance("USD", 1250m, 2), new CurrencyBalance("EUR", 250m, 2)])
            .Should().Equal(
                new BalanceLine($"{MoneyFormatter.Format(1250m, "USD")} USD", "USD"),
                new BalanceLine($"{MoneyFormatter.Format(250m, "EUR")} EUR", "EUR"));
    }

    [Fact]
    public void No_accounts_show_zero_for_all_currencies()
    {
        CurrencyBalance.DisplayLines([]).Should().Equal(new BalanceLine(MoneyFormatter.Format(0), null));
    }
}
