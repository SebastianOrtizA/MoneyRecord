using AwesomeAssertions;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using Xunit;

namespace MoneyRecord.Tests.Helpers;

public class CurrencyConverterTests
{
    private static readonly DateTime Oct1 = new(2026, 10, 1);

    private static ExchangeRate Rate(string baseCode, string quote, DateTime date, double rate) =>
        new() { BaseCode = baseCode, QuoteCode = quote, Date = date, Rate = rate };

    private static readonly CurrencyConverter Usd = new(
    [
        Rate("USD", "COP", Oct1, 4000),
        Rate("USD", "COP", Oct1.AddDays(2), 3200),
        Rate("USD", "EUR", Oct1, 0.8)
    ]);

    [Fact]
    public void Uses_the_rate_of_the_date_or_the_nearest_earlier_day()
    {
        Usd.Convert(8000m, "COP", Oct1, "USD").Should().Be(2m);
        Usd.Convert(8000m, "COP", Oct1.AddDays(1).AddHours(15), "USD").Should().Be(2m, "Oct 2 has no rate: Oct 1's");
        Usd.Convert(8000m, "COP", Oct1.AddDays(9), "USD").Should().Be(2.5m);
    }

    [Fact]
    public void A_date_before_every_rate_or_an_unknown_currency_cant_be_converted()
    {
        Usd.Convert(10m, "COP", Oct1.AddDays(-1), "USD").Should().BeNull();
        Usd.Convert(10m, "JPY", Oct1, "USD").Should().BeNull();
        CurrencyConverter.Empty.HasRates.Should().BeFalse();
    }

    [Fact]
    public void Converts_both_ways_and_across_the_base_rates_were_downloaded_for()
    {
        Usd.Convert(2m, "USD", Oct1, "COP").Should().Be(8000m);
        Usd.Convert(4000m, "COP", Oct1, "EUR").Should().Be(0.8m, "COP → USD → EUR, after the base changed to EUR");
        Usd.Convert(5m, "eur", Oct1, "eur").Should().Be(5m);
    }

    [Fact]
    public void A_transaction_uses_its_own_conversion_only_when_it_is_in_the_wanted_currency()
    {
        var lunch = new Transaction { Amount = 40_000m, Date = Oct1, ConvertedAmount = 9.5m, ConvertedCurrencyCode = "USD" };

        Usd.Convert(lunch, "COP", "USD").Should().Be(9.5m);
        Usd.Convert(lunch, "COP", "EUR").Should().Be(8m, "a conversion into another currency is ignored");
        Usd.Convert(new Transaction { Amount = 40_000m, Date = Oct1 }, "COP", "USD").Should().Be(10m);
    }
}

public class TransactionConversionTests
{
    [Fact]
    public void An_amount_in_the_base_is_kept_and_an_empty_field_clears_only_a_conversion_into_the_base()
    {
        var lunch = new Transaction();

        lunch.SetConversion(9.5m, "COP", "usd");
        (lunch.ConvertedAmount, lunch.ConvertedCurrencyCode).Should().Be((9.5m, "USD"));

        lunch.SetConversion(null, "COP", "USD");
        lunch.ConvertedAmount.Should().BeNull();

        var older = new Transaction { ConvertedAmount = 8m, ConvertedCurrencyCode = "EUR" };
        older.SetConversion(null, "COP", "USD");
        (older.ConvertedAmount, older.ConvertedCurrencyCode).Should().Be((8m, "EUR"), "the field showed USD, not this one");
    }

    [Fact]
    public void An_account_in_the_base_currency_needs_no_conversion()
    {
        var lunch = new Transaction { ConvertedAmount = 8m, ConvertedCurrencyCode = "EUR" };

        lunch.SetConversion(5m, "USD", "USD");

        lunch.ConvertedAmount.Should().BeNull();
        lunch.ConvertedCurrencyCode.Should().BeNull();
    }
}
