using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.Services.Repositories;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SQLite;
using Xunit;

namespace MoneyRecord.Tests.Services;

/// <summary>
/// The rate update against a real temp SQLite file (coverage and first-transaction queries are SQL) and a fake source.
/// The base currency is pinned to USD (tests run sequentially).
/// </summary>
public sealed class ExchangeRateServiceTests : IAsyncLifetime
{
    private static readonly DateTime Today = new(2026, 10, 15);

    /// <summary>Rates always reach back to the first of the month 12 months ago (Net worth's longest window).</summary>
    private static readonly DateTime History = new(2025, 10, 1);

    private readonly string _appData = Path.Combine(Path.GetTempPath(), $"moneyrecord-rates-{Guid.NewGuid():N}");
    private readonly IExchangeRateSource _source = Substitute.For<IExchangeRateSource>();
    private readonly ExchangeRateSettings _settings = new(new FakePreferences());
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(Today.AddHours(9), TimeSpan.Zero));
    private readonly string _previousBase = MoneyFormatter.BaseCurrencyCode;
    private DatabaseInitializer _database = null!;
    private ExchangeRateRepository _rates = null!;
    private AccountRepository _accounts = null!;
    private ExchangeRateService _sut = null!;

    private SQLiteAsyncConnection Db { get; set; } = null!;

    public async Task InitializeAsync()
    {
        MoneyFormatter.BaseCurrencyCode = "USD";
        _clock.SetLocalTimeZone(TimeZoneInfo.Utc);
        Directory.CreateDirectory(_appData);
        _database = new DatabaseInitializer(new AppPaths(_appData));
        Db = await _database.GetConnectionAsync();
        _rates = new ExchangeRateRepository(_database);
        _accounts = new AccountRepository(_database);
        _sut = new ExchangeRateService(_rates, _accounts, new TransactionRepository(_database),
            _source, _settings, _clock, NullLogger<ExchangeRateService>.Instance);

        // The source answers with one rate per day asked for
        _source.GetRatesAsync(default!, default!, default, default).ReturnsForAnyArgs(call =>
        {
            var (baseCode, quote, from, to) = (call.ArgAt<string>(0), call.ArgAt<string>(1), call.ArgAt<DateTime>(2), call.ArgAt<DateTime>(3));
            return Enumerable.Range(0, (to - from).Days + 1)
                .Select(d => new ExchangeRate { BaseCode = baseCode, QuoteCode = quote, Date = from.AddDays(d), Rate = 4000 })
                .ToList();
        });
    }

    public async Task DisposeAsync()
    {
        MoneyFormatter.BaseCurrencyCode = _previousBase;
        await Db.CloseAsync();
        Directory.Delete(_appData, recursive: true);
    }

    private async Task<Account> AccountAsync(string currency)
    {
        var account = new Account { Name = currency, CurrencyCode = currency };
        await _accounts.SaveAsync(account); // through the repository, which caches the accounts
        return account;
    }

    [Fact]
    public async Task Downloads_each_other_currency_from_a_year_back_or_its_older_first_transaction_to_today()
    {
        var pesos = await AccountAsync("COP");
        await AccountAsync("EUR"); // no transactions: the year back
        await Db.InsertAsync(new Transaction { AccountId = pesos.Id, Amount = 1m, Date = History.AddDays(-3).AddHours(14) });

        var result = await _sut.UpdateAsync();

        result.Unavailable.Should().BeEmpty();
        result.Failed.Should().BeEmpty();
        var days = (Today - History).Days + 1;
        result.Saved.Should().Be(days + 3 + days);
        await _source.Received(1).GetRatesAsync("USD", "COP", History.AddDays(-3), Today, Arg.Any<CancellationToken>());
        await _source.Received(1).GetRatesAsync("USD", "EUR", History, Today, Arg.Any<CancellationToken>());
        _settings.LastUpdated.Should().Be(Today.AddHours(9));
    }

    [Fact]
    public async Task A_later_update_only_asks_for_the_days_missing_before_and_after_the_stored_ones()
    {
        var pesos = await AccountAsync("COP");
        await _rates.SaveRatesAsync([new ExchangeRate { BaseCode = "USD", QuoteCode = "COP", Date = Today.AddDays(-5), Rate = 1 },
            new ExchangeRate { BaseCode = "USD", QuoteCode = "COP", Date = Today.AddDays(-2), Rate = 1 }]);
        await Db.InsertAsync(new Transaction { AccountId = pesos.Id, Amount = 1m, Date = Today.AddDays(-10) });

        await _sut.UpdateAsync();

        await _source.Received(1).GetRatesAsync("USD", "COP", History, Today.AddDays(-6), Arg.Any<CancellationToken>());
        await _source.Received(1).GetRatesAsync("USD", "COP", Today.AddDays(-1), Today, Arg.Any<CancellationToken>());
        (await _rates.GetCoverageAsync("usd", "cop")).Should().Be((History, Today));
    }

    [Fact]
    public async Task A_stored_day_gets_the_new_rate_instead_of_a_second_row()
    {
        await _rates.SaveRatesAsync([new ExchangeRate { BaseCode = "USD", QuoteCode = "COP", Date = Today, Rate = 1 }]);
        await _rates.SaveRatesAsync([new ExchangeRate { BaseCode = "USD", QuoteCode = "COP", Date = Today.AddHours(5), Rate = 2 }]);

        var stored = await _rates.GetAllAsync();

        stored.Should().ContainSingle().Which.Rate.Should().Be(2);
    }

    [Fact]
    public async Task An_unknown_currency_is_reported_and_a_failed_download_is_retried_next_time()
    {
        await AccountAsync("XTS");
        await AccountAsync("COP");
        _source.GetRatesAsync("USD", "XTS", Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ExchangeRateUnavailableException("XTS"));
        _source.GetRatesAsync("USD", "COP", Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("offline"));

        var result = await _sut.UpdateAsync();

        result.Unavailable.Should().Equal("XTS");
        result.Failed.Should().Equal("COP");
        _settings.LastUpdated.Should().BeNull("an update that failed for lack of a connection runs again");
    }

    [Fact]
    public async Task The_due_update_runs_only_when_turned_on_and_once_a_day()
    {
        await AccountAsync("COP");

        await _sut.UpdateIfDueAsync();
        _source.ReceivedCalls().Should().BeEmpty("the download is off until the user turns it on");

        _settings.Enabled = true;
        await _sut.UpdateIfDueAsync();
        await _sut.UpdateIfDueAsync();

        _source.ReceivedCalls().Should().ContainSingle();
    }

    [Fact]
    public async Task The_converter_sees_the_rates_of_the_latest_update()
    {
        await AccountAsync("COP");
        (await _sut.GetConverterAsync()).Convert(8000m, "COP", Today, "USD").Should().BeNull();

        await _sut.UpdateAsync();

        (await _sut.GetConverterAsync()).Convert(8000m, "COP", Today, "USD").Should().Be(2m);
    }

    [Fact]
    public async Task After_a_save_the_update_runs_even_when_it_already_ran_today()
    {
        await AccountAsync("COP");
        _settings.Enabled = true;
        await _sut.UpdateIfDueAsync();
        await AccountAsync("EUR"); // a new currency the same day

        await _sut.UpdateIfDueAsync();
        await _sut.UpdateIfEnabledAsync();

        await _source.Received(1).GetRatesAsync("USD", "EUR", History, Today, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_range_before_the_stored_days_that_came_back_empty_isnt_asked_again()
    {
        var pesos = await AccountAsync("COP");
        await _rates.SaveRatesAsync([new ExchangeRate { BaseCode = "USD", QuoteCode = "COP", Date = Today, Rate = 1 }]);
        await Db.InsertAsync(new Transaction { AccountId = pesos.Id, Amount = 1m, Date = new DateTime(1900, 1, 1) });
        _source.GetRatesAsync("USD", "COP", new DateTime(1900, 1, 1), Today.AddDays(-1), Arg.Any<CancellationToken>())
            .Returns(new List<ExchangeRate>()); // nothing that old

        await _sut.UpdateAsync();
        await _sut.UpdateAsync();

        await _source.Received(1).GetRatesAsync("USD", "COP", new DateTime(1900, 1, 1), Today.AddDays(-1), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Missing_ranges_cover_the_days_around_what_is_stored()
    {
        ExchangeRateService.MissingRanges(null, Today.AddDays(-3), Today).Should().Equal((Today.AddDays(-3), Today));
        ExchangeRateService.MissingRanges((Today.AddDays(-3), Today), Today.AddDays(-1), Today).Should().BeEmpty();
    }
}
