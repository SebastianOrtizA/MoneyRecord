using AwesomeAssertions;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;
using NSubstitute;
using Xunit;

namespace MoneyRecord.Tests.Services;

/// <summary>The "Show in" switch applied to totals. The base currency is pinned to USD (tests run sequentially).</summary>
public sealed class ConversionServiceTests : IDisposable
{
    private static readonly DateTime Oct1 = new(2026, 10, 1);
    private static readonly Account Cash = new() { Id = 1, CurrencyCode = "USD" };
    private static readonly Account Pesos = new() { Id = 2, CurrencyCode = "COP" };
    private static readonly Account Yen = new() { Id = 3, CurrencyCode = "JPY" }; // no rates downloaded

    private readonly string _previousBase = MoneyFormatter.BaseCurrencyCode;
    private readonly IExchangeRateService _rates = Substitute.For<IExchangeRateService>();
    private readonly ExchangeRateSettings _rateSettings = new(new FakePreferences()) { Enabled = true };
    private readonly CurrencyDisplaySettings _display = new(new FakePreferences());
    private readonly IAccountRepository _accounts = Substitute.For<IAccountRepository>();
    private readonly ITransactionRepository _transactions = Substitute.For<ITransactionRepository>();
    private readonly IBalanceService _balances = Substitute.For<IBalanceService>();
    private readonly ConversionService _sut;

    public ConversionServiceTests()
    {
        MoneyFormatter.BaseCurrencyCode = "USD";
        _rates.GetConverterAsync().Returns(new CurrencyConverter(
            [new ExchangeRate { BaseCode = "USD", QuoteCode = "COP", Date = Oct1, Rate = 4000 }]));
        _accounts.GetAllAsync().Returns([Cash, Pesos, Yen]);
        _sut = new ConversionService(_rates, _rateSettings, _display, _accounts, _transactions, _balances);
    }

    public void Dispose() => MoneyFormatter.BaseCurrencyCode = _previousBase;

    private static Transaction Entry(Account account, TransactionType type, decimal amount, int categoryId = 1) =>
        new() { AccountId = account.Id, Type = type, Amount = amount, Date = Oct1, CategoryId = categoryId };

    [Fact]
    public async Task Totals_are_converted_once_rates_exist_unless_the_user_chose_by_currency()
    {
        (await _sut.IsConvertedAsync()).Should().BeTrue("not chosen yet: converted once rates exist");

        _sut.SetConverted(false);
        (await _sut.IsConvertedAsync()).Should().BeFalse();

        _sut.SetConverted(true);
        _rateSettings.Enabled = false;
        (await _sut.IsConvertedAsync()).Should().BeFalse("without the download nothing converts");
        (await _sut.CanConvertAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Converts_with_the_rate_or_the_entrys_own_amount_and_keeps_the_rest_in_its_currency()
    {
        var own = Entry(Pesos, TransactionType.Expense, 40_000m);
        own.ConvertedAmount = 9.5m;
        own.ConvertedCurrencyCode = "USD";

        var result = await _sut.ConvertAsync(
            [Entry(Cash, TransactionType.Expense, 5m), Entry(Pesos, TransactionType.Expense, 8000m), own,
             Entry(Yen, TransactionType.Expense, 1000m), new Transaction { Amount = -3m, Date = Oct1 }], [Cash, Pesos, Yen]);

        result.Converted.Select(t => (t.Amount, t.CurrencyCode)).Should().Equal(
            (5m, "USD"), (2m, "USD"), (9.5m, "USD"), (3m, "USD"));
        result.FromOtherCurrencies.Should().Be(2);
        result.NotConvertedTotals("USD").Should().Equal(new CurrencyAmount("JPY", 1000m));
    }

    [Fact]
    public async Task Home_shows_one_converted_total_marked_approximate_and_lists_what_had_no_rate()
    {
        _transactions.GetByDateRangeAsync(default, default).ReturnsForAnyArgs(
        [
            Entry(Cash, TransactionType.Income, 100m), Entry(Pesos, TransactionType.Expense, 80_000m),
            Entry(Yen, TransactionType.Expense, 500m)
        ]);

        var totals = await _sut.GetPeriodTotalsAsync(Oct1, Oct1.AddDays(30));

        totals.IsConverted.Should().BeTrue();
        (totals.Base.Income, totals.Base.Expense, totals.Base.IsApproximate).Should().Be((100m, 20m, true));
        totals.Base.ApproximateMark.Should().Be("≈ ");
        totals.Others.Should().ContainSingle().Which.Should().Be(new PeriodTotals("JPY", 0, 500m));
    }

    [Fact]
    public async Task By_currency_Home_lists_each_currency_from_the_SQL_totals()
    {
        _sut.SetConverted(false);
        _balances.GetTotalIncomesAsync(default, default).ReturnsForAnyArgs([new CurrencyAmount("USD", 100m)]);
        _balances.GetTotalExpensesAsync(default, default).ReturnsForAnyArgs([new CurrencyAmount("COP", 8000m)]);

        var totals = await _sut.GetPeriodTotalsAsync(Oct1, Oct1.AddDays(30));

        totals.IsConverted.Should().BeFalse();
        totals.Base.Should().Be(new PeriodTotals("USD", 100m, 0));
        totals.Others.Should().Equal(new PeriodTotals("COP", 0, 8000m));
    }

    [Fact]
    public async Task Budgets_count_converted_spending_and_list_what_had_no_rate()
    {
        _transactions.GetByDateRangeAsync(default, default).ReturnsForAnyArgs(
        [
            Entry(Cash, TransactionType.Expense, 10m), Entry(Pesos, TransactionType.Expense, 8000m),
            Entry(Yen, TransactionType.Expense, 700m), Entry(Pesos, TransactionType.Income, 4000m)
        ]);

        var spent = await _sut.GetExpensesByCategoryAsync(Oct1, Oct1.AddDays(30));

        spent[1].Counted.Should().Be(12m);
        spent[1].Uncounted.Should().Equal(new CurrencyAmount("JPY", 700m));
    }

    [Fact]
    public async Task By_currency_budgets_count_only_the_base_currency()
    {
        _sut.SetConverted(false);
        _balances.GetExpensesByCategoryAsync(default, default).ReturnsForAnyArgs(new Dictionary<int, List<CurrencyAmount>>
        {
            [1] = [new("USD", 10m), new("COP", 8000m)]
        });

        var spent = await _sut.GetExpensesByCategoryAsync(Oct1, Oct1.AddDays(30));

        spent[1].Should().BeEquivalentTo(new BudgetSpent(10m, [new CurrencyAmount("COP", 8000m)]));
    }

    [Fact]
    public async Task A_report_by_currency_keeps_that_currencys_entries_only()
    {
        var report = await _sut.ForReportAsync(
            [Entry(Cash, TransactionType.Expense, 5m), Entry(Pesos, TransactionType.Expense, 8000m)], [Cash, Pesos],
            converted: false, currencyCode: "COP");

        report.Transactions.Should().ContainSingle().Which.Amount.Should().Be(8000m);
        (report.CurrencyCode, report.IsApproximate, report.NotConverted.Count).Should().Be(("COP", false, 0));
    }
}
