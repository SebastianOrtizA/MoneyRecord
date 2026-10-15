using AwesomeAssertions;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using Xunit;

namespace MoneyRecord.Tests.Helpers;

public class ReportCurrenciesTests
{
    private static readonly List<Account> Accounts =
    [
        new() { Id = 1, CurrencyCode = "USD" },
        new() { Id = 2, CurrencyCode = "EUR" },
        new() { Id = 3, CurrencyCode = "COP" },
        new() { Id = 4, CurrencyCode = "usd" }
    ];

    [Fact]
    public void Lists_the_base_currency_first_even_without_an_account_in_it()
    {
        ReportCurrencies.InUse(Accounts, "cop").Should().Equal("COP", "EUR", "USD");
        ReportCurrencies.InUse([], "JPY").Should().Equal("JPY");
    }

    [Fact]
    public void Keeps_the_transactions_of_one_currency_and_counts_no_account_as_the_base()
    {
        var transactions = new List<Transaction>
        {
            new() { Id = 1, AccountId = 1 },
            new() { Id = 2, AccountId = 2 },
            new() { Id = 3, AccountId = null },
            new() { Id = 4, AccountId = 99 }, // account gone
            new() { Id = 5, AccountId = 4 }
        };

        ReportCurrencies.Only(transactions, Accounts, "USD", "COP").Select(t => t.Id).Should().Equal(1, 5);
        ReportCurrencies.Only(transactions, Accounts, "COP", "COP").Select(t => t.Id).Should().Equal(3, 4);
    }
}
