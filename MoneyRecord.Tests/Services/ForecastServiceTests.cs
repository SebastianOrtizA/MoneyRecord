using AwesomeAssertions;
using Microsoft.Extensions.Time.Testing;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.Services.Repositories;
using NSubstitute;
using SQLite;
using Xunit;

namespace MoneyRecord.Tests.Services;

/// <summary>
/// ForecastService against a real temp SQLite file, with the clock fixed at 10 November 2026 10:00 and the base currency
/// pinned to USD (tests run sequentially).
/// </summary>
public sealed class ForecastServiceTests : IAsyncLifetime
{
    private static readonly DateTime Today = new(2026, 11, 10);

    private readonly string _appData = Path.Combine(Path.GetTempPath(), $"moneyrecord-forecast-{Guid.NewGuid():N}");
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 11, 10, 10, 0, 0, TimeSpan.Zero));
    private readonly string _previousBase = MoneyFormatter.BaseCurrencyCode;
    private readonly IConversionService _conversion = Substitute.For<IConversionService>();
    private readonly IExchangeRateService _rates = Substitute.For<IExchangeRateService>();
    private DatabaseInitializer _database = null!;
    private ForecastService _sut = null!;
    private Account _cash = null!;

    private SQLiteAsyncConnection Db { get; set; } = null!;

    public async Task InitializeAsync()
    {
        MoneyFormatter.BaseCurrencyCode = "USD";
        Directory.CreateDirectory(_appData);
        _database = new DatabaseInitializer(new AppPaths(_appData));
        Db = await _database.GetConnectionAsync();

        var accounts = new AccountRepository(_database);
        var transactions = new TransactionRepository(_database);
        var transfers = new TransferRepository(_database);
        var recurringRepo = new RecurringTransactionRepository(_database);
        var recurring = new RecurringTransactionService(recurringRepo, new CategoryRepository(_database), accounts, _database, _clock);
        _conversion.GetStateAsync().Returns(ConversionState.Off);
        _rates.GetConverterAsync().Returns(new CurrencyConverter(
            [new ExchangeRate { BaseCode = "USD", QuoteCode = "COP", Date = new DateTime(2026, 11, 1), Rate = 4000 }]));
        _sut = new ForecastService(accounts, new BalanceService(accounts, transactions, transfers, _clock), recurring,
            recurringRepo, transactions, transfers, _conversion, _rates, _clock);

        // The database seeds a default Cash account: it is the one with 1,000
        _cash = await Db.Table<Account>().FirstAsync();
        _cash.CurrencyCode = "USD";
        _cash.InitialBalance = 1000m;
        await Db.UpdateAsync(_cash);
    }

    public async Task DisposeAsync()
    {
        MoneyFormatter.BaseCurrencyCode = _previousBase;
        await Db.CloseAsync();
        Directory.Delete(_appData, recursive: true);
    }

    private async Task<Account> AddAccountAsync(string name, string currency, decimal balance, bool isDebt = false)
    {
        var account = new Account { Name = name, CurrencyCode = currency, InitialBalance = balance, IsDebt = isDebt };
        await Db.InsertAsync(account);
        return account;
    }

    private async Task<RecurringTransaction> AddRuleAsync(RecurrenceFrequency frequency, DateTime next, decimal amount,
        Account? account = null, TransactionType type = TransactionType.Expense, DateTime? end = null, bool active = true,
        int? dayOfMonth = null, bool variable = false, int? debtId = null)
    {
        var rule = new RecurringTransaction
        {
            Amount = amount, Description = "Rule", AccountId = (account ?? _cash).Id, TransactionType = type,
            Frequency = frequency, DayOfMonth = dayOfMonth, StartDate = next, NextOccurrence = next, EndDate = end,
            IsActive = active, IsVariableAmount = variable, LastAmountCents = variable ? Money.ToCents(amount) : 0, DebtId = debtId
        };
        await Db.InsertAsync(rule);
        return rule;
    }

    [Fact]
    public async Task Starts_from_the_counted_balance_and_leaves_out_hidden_debt_accounts()
    {
        await AddAccountAsync("Ana", "USD", 300m, isDebt: true);
        await Db.InsertAsync(new Transaction { AccountId = _cash.Id, Type = TransactionType.Expense, Amount = 100m, Date = Today });

        var result = await _sut.GetForecastAsync(30);

        result.Accounts.Should().ContainSingle().Which.Series.Points[0].Cents.Should().Be(90_000);
        result.Currencies.Should().ContainSingle().Which.Series.EndCents.Should().Be(90_000);
        result.Currencies[0].Series.Points.Should().HaveCount(31);
    }

    [Fact]
    public async Task Rent_on_the_31st_lands_on_the_last_day_of_short_months()
    {
        await AddRuleAsync(RecurrenceFrequency.Monthly, new DateTime(2026, 11, 30), 400m, dayOfMonth: 31);

        var result = await _sut.GetForecastAsync(90);

        result.Events.Select(e => e.Date).Should().Equal(new DateTime(2026, 11, 30), new DateTime(2026, 12, 31), new DateTime(2027, 1, 31));
        result.Events.Should().AllSatisfy(e => e.Cents.Should().Be(-40_000));
        result.Currencies[0].Series.EndCents.Should().Be(100_000 - 120_000);
    }

    [Fact]
    public async Task Weekly_rules_repeat_across_the_horizon_and_stop_at_their_end_date()
    {
        await AddRuleAsync(RecurrenceFrequency.Weekly, new DateTime(2026, 11, 12), 10m);
        await AddRuleAsync(RecurrenceFrequency.Weekly, new DateTime(2026, 11, 11), 5m, type: TransactionType.Income, end: new DateTime(2026, 11, 20));

        var result = await _sut.GetForecastAsync(30);

        result.Events.Count(e => e.Cents == -1000).Should().Be(5, "Nov 12, 19, 26, Dec 3 and 10");
        result.Events.Count(e => e.Cents == 500).Should().Be(2, "Nov 11 and 18; the 25th is past the end date");
    }

    [Fact]
    public async Task An_inactive_rule_has_no_events()
    {
        await AddRuleAsync(RecurrenceFrequency.Weekly, new DateTime(2026, 11, 12), 10m, active: false);

        (await _sut.GetForecastAsync(30)).Events.Should().BeEmpty();
    }

    [Fact]
    public async Task A_variable_rule_is_an_estimate_and_an_overdue_one_counts_today()
    {
        await AddRuleAsync(RecurrenceFrequency.Monthly, new DateTime(2026, 11, 8), 85m, variable: true);

        var result = await _sut.GetForecastAsync(30);

        var overdue = result.Events.First();
        overdue.IsEstimate.Should().BeTrue();
        overdue.Cents.Should().Be(-8500);
        result.Currencies[0].Series.Points[0].Cents.Should().Be(100_000 - 8500, "the overdue occurrence is counted today");
    }

    [Fact]
    public async Task Entries_dated_after_today_count_from_their_day_but_those_up_to_today_are_not_added_again()
    {
        var other = await AddAccountAsync("Savings", "USD", 0m);
        await Db.InsertAsync(new Transaction { AccountId = _cash.Id, Type = TransactionType.Expense, Amount = 50m, Date = Today });
        await Db.InsertAsync(new Transaction { AccountId = _cash.Id, Type = TransactionType.Expense, Amount = 200m, Date = Today.AddDays(3) });
        await Db.InsertAsync(new Transfer
        {
            SourceAccountId = _cash.Id, DestinationAccountId = other.Id, Amount = 100m, DestinationAmount = 100m, Date = Today.AddDays(5)
        });

        var result = await _sut.GetForecastAsync(30);

        result.Events.Should().HaveCount(3, "one expense and the two sides of the transfer");
        var cash = result.Accounts.Single(a => a.AccountId == _cash.Id).Series;
        cash.Points[0].Cents.Should().Be(95_000);
        cash.Points[3].Cents.Should().Be(75_000);
        cash.EndCents.Should().Be(65_000);
        result.Accounts.Single(a => a.AccountId == other.Id).Series.EndCents.Should().Be(10_000);
    }

    [Fact]
    public async Task A_rule_and_a_future_dated_entry_on_the_same_day_each_count_once()
    {
        await AddRuleAsync(RecurrenceFrequency.Monthly, Today.AddDays(2), 30m);
        await Db.InsertAsync(new Transaction { AccountId = _cash.Id, Type = TransactionType.Expense, Amount = 30m, Date = Today.AddDays(2) });

        var result = await _sut.GetForecastAsync(30);

        result.Events.Should().HaveCount(2);
        result.Currencies[0].Series.EndCents.Should().Be(100_000 - 6000);
    }

    [Fact]
    public async Task A_debt_payment_with_nothing_left_has_no_events()
    {
        await AddRuleAsync(RecurrenceFrequency.Monthly, Today.AddDays(2), 30m, debtId: 99);

        (await _sut.GetForecastAsync(30)).Events.Should().BeEmpty();
    }

    [Fact]
    public async Task Currencies_are_never_added_and_the_converted_total_appears_only_when_the_switch_converts()
    {
        var pesos = await AddAccountAsync("Pesos", "COP", 400_000m);
        await AddRuleAsync(RecurrenceFrequency.Monthly, Today.AddDays(1), 80_000m, pesos);

        var result = await _sut.GetForecastAsync(30);

        result.Currencies.Select(c => c.CurrencyCode).Should().Equal("USD", "COP");
        result.Currencies[0].Series.EndCents.Should().Be(100_000);
        result.Currencies[1].Series.EndCents.Should().Be(32_000_000);
        result.Converted.Should().BeNull();
        result.NotConverted.Should().BeEmpty();
    }

    [Fact]
    public async Task Converting_uses_each_days_rate_and_reports_what_has_no_rate()
    {
        var pesos = await AddAccountAsync("Pesos", "COP", 400_000m);
        var yen = await AddAccountAsync("Yen", "JPY", 5000m);
        await AddRuleAsync(RecurrenceFrequency.Monthly, Today.AddDays(1), 80_000m, pesos);
        await AddRuleAsync(RecurrenceFrequency.Monthly, Today.AddDays(2), 1000m, yen);
        _conversion.GetStateAsync().Returns(new ConversionState(true, true));

        var result = await _sut.GetForecastAsync(30);

        result.Converted.Should().NotBeNull();
        result.Converted!.CurrencyCode.Should().Be("USD");
        result.Converted.IsApproximate.Should().BeTrue();
        // 1000 USD + 400000 COP / 4000 = 1100; the 80000 COP payment is 20 USD
        result.Converted.Series.Points[0].Cents.Should().Be(110_000);
        result.Converted.Series.EndCents.Should().Be(108_000);
        result.NotConverted.Should().Equal(new CurrencyAmount("JPY", 4000m)); // 5000 yen less the 1000 payment, neither has a rate
    }
}
