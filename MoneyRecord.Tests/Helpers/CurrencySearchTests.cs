using AwesomeAssertions;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using Xunit;

namespace MoneyRecord.Tests.Helpers;

public class CurrencySearchTests
{
    private static readonly List<Currency> All =
    [
        new() { Code = "AUD", Symbol = "$", Name = "Australian Dollar" },
        new() { Code = "COP", Symbol = "$", Name = "Colombian Peso" },
        new() { Code = "EUR", Symbol = "€", Name = "Euro" },
        new() { Code = "SEK", Symbol = "kr", Name = "Swedish Krona" },
        new() { Code = "STN", Symbol = "STN", Name = "São Tomé and Príncipe Dobra" },
        new() { Code = "USD", Symbol = "$", Name = "US Dollar" },
    ];

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Nothing_typed_lists_every_currency_in_order(string? query)
    {
        CurrencySearch.Filter(All, query).Select(c => c.Code).Should().Equal("AUD", "COP", "EUR", "SEK", "STN", "USD");
    }

    [Theory]
    [InlineData("eur", "EUR")]
    [InlineData("colom", "COP")]
    [InlineData("sao tome", "STN")]
    [InlineData("€", "EUR")]
    [InlineData("kr", "SEK")]
    public void Matches_the_code_the_name_or_the_symbol_ignoring_case_and_accents(string query, string expected)
    {
        CurrencySearch.Filter(All, query).Select(c => c.Code).Should().Equal(expected);
    }

    [Fact]
    public void A_code_that_starts_with_the_text_comes_first()
    {
        // "us" is inside "Australian" (AUD) and the start of USD
        CurrencySearch.Filter(All, "us").Select(c => c.Code).Should().Equal("USD", "AUD");
    }

    [Fact]
    public void Nothing_matching_lists_nothing()
    {
        CurrencySearch.Filter(All, "zzz").Should().BeEmpty();
    }
}
