using AwesomeAssertions;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.ViewModels;
using NSubstitute;
using Xunit;

namespace MoneyRecord.Tests.ViewModels;

/// <summary>The Forecast page's view model against a substituted <see cref="IForecastService"/> (base currency pinned to USD).</summary>
public sealed class ForecastViewModelTests : IDisposable
{
    private static readonly DateTime Today = new(2026, 11, 10);

    private readonly string _previousBase = MoneyFormatter.BaseCurrencyCode;
    private readonly IForecastService _forecast = Substitute.For<IForecastService>();
    private readonly IConversionService _conversion = Substitute.For<IConversionService>();
    private readonly IErrorHandler _errors = Substitute.For<IErrorHandler>();
    private readonly PeriodViewModel _period;
    private readonly ForecastViewModel _sut;

    public ForecastViewModelTests()
    {
        MoneyFormatter.BaseCurrencyCode = "USD";
        _conversion.GetStateAsync().Returns(ConversionState.Off);
        _period = new PeriodViewModel(_conversion);
        _sut = new ForecastViewModel(_forecast, _period, _conversion, _errors, Substitute.For<INavigationService>());
    }

    public void Dispose() => MoneyFormatter.BaseCurrencyCode = _previousBase;

    private static ForecastSeries Series(int days, long start, params (int Day, long Cents)[] changes)
    {
        var balance = start;
        var points = new List<ForecastPoint>();
        for (var i = 0; i <= days; i++)
        {
            balance += changes.Where(c => c.Day == i).Sum(c => c.Cents);
            points.Add(new ForecastPoint(Today.AddDays(i), balance));
        }

        return new ForecastSeries(points);
    }

    private static ForecastResult Result(int days, params AccountForecast[] accounts)
    {
        var currencies = accounts.GroupBy(a => a.CurrencyCode)
            .Select(g => new CurrencyForecast(g.Key, ForecastSeries.Combine(g.Select(a => a.Series).ToList())))
            .OrderBy(c => c.CurrencyCode == "USD" ? 0 : 1).ToList();
        return new ForecastResult(Today, days, accounts, currencies, null, [], []);
    }

    [Fact]
    public async Task Load_shows_the_base_currency_total_with_its_lowest_day()
    {
        var checking = new AccountForecast(1, "Checking", "USD", Series(30, 10_000, (5, -15_000), (20, 30_000)));
        var savings = new AccountForecast(2, "Savings", "USD", Series(30, 50_000));
        _forecast.GetForecastAsync(30, Arg.Any<CancellationToken>()).Returns(Result(30, checking, savings));

        await _sut.InitializeAsync();

        _sut.HasData.Should().BeTrue();
        _sut.View!.CurrencyCode.Should().Be("USD");
        _sut.View.Values.Should().HaveCount(31);
        _sut.View.Lowest.Date.Should().Be(Today.AddDays(5));
        _sut.View.Lowest.Value.Should().Be(450m);
        _sut.View.GoesNegative.Should().BeFalse();
        _sut.LowestText.Should().Contain("450");
    }

    [Fact]
    public async Task Choosing_days_asks_the_service_for_that_horizon()
    {
        _forecast.GetForecastAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(call => Result(call.Arg<int>(), new AccountForecast(1, "Checking", "USD", Series(call.Arg<int>(), 1_000))));
        await _sut.InitializeAsync();

        _sut.SelectDaysCommand.Execute(_sut.DayChoices.Single(d => d.Days == 90));

        await _forecast.Received().GetForecastAsync(90, Arg.Any<CancellationToken>());
        _sut.Days.Should().Be(90);
        _sut.DayChoices.Single(d => d.IsSelected).Days.Should().Be(90);
    }

    [Fact]
    public async Task A_currency_chip_shows_only_that_currencys_accounts_and_events()
    {
        var usd = new AccountForecast(1, "Checking", "USD", Series(30, 10_000));
        var cop = new AccountForecast(2, "Pesos", "COP", Series(30, 5_000_000, (3, -100_000)));
        var result = Result(30, usd, cop) with
        {
            Events =
            [
                new ForecastEvent(Today.AddDays(2), 1, -500, ForecastSource.Recurring, false, "Rent", 7),
                new ForecastEvent(Today.AddDays(3), 2, -100_000, ForecastSource.Scheduled, true, "Gift")
            ]
        };
        _forecast.GetForecastAsync(30, Arg.Any<CancellationToken>()).Returns(result);
        await _sut.InitializeAsync();
        _sut.Events.Select(e => e.Description).Should().Equal("Rent");

        _period.SelectCurrencyCommand.Execute(_period.CurrencyChoices.Single(c => c.Code == "COP"));
        await _sut.InitializeAsync();

        _sut.View!.CurrencyCode.Should().Be("COP");
        _sut.Events.Select(e => e.Description).Should().Equal("Gift");
        _sut.Events[0].AmountText.Should().StartWith("~", "an estimate is marked");
    }

    [Fact]
    public async Task Choosing_an_account_draws_its_own_line_and_events()
    {
        var checking = new AccountForecast(1, "Checking", "USD", Series(30, 10_000, (4, -12_000)));
        var savings = new AccountForecast(2, "Savings", "USD", Series(30, 50_000));
        var result = Result(30, checking, savings) with
        {
            Events = [new ForecastEvent(Today.AddDays(4), 1, -12_000, ForecastSource.Recurring, false, "Rent", 7)]
        };
        _forecast.GetForecastAsync(30, Arg.Any<CancellationToken>()).Returns(result);
        await _sut.InitializeAsync();
        _sut.HasAccountChoices.Should().BeTrue();

        _sut.SelectedAccount = _sut.AccountOptions.Single(o => o.AccountId == 1);
        await _sut.LoadCommand.ExecuteAsync(null);

        _sut.SelectedAccount!.AccountId.Should().Be(1);
        _sut.View!.GoesNegative.Should().BeTrue();
        _sut.View.Lowest.Value.Should().Be(-20m);
        _sut.Events.Should().ContainSingle();
    }

    [Fact]
    public async Task A_slow_older_load_never_overwrites_a_newer_one()
    {
        var gate = new TaskCompletionSource<ForecastResult>();
        _forecast.GetForecastAsync(30, Arg.Any<CancellationToken>()).Returns(call => gate.Task.WaitAsync(call.Arg<CancellationToken>()));
        _forecast.GetForecastAsync(60, Arg.Any<CancellationToken>())
            .Returns(Result(60, new AccountForecast(1, "Checking", "USD", Series(60, 1_000))));

        var first = _sut.LoadCommand.ExecuteAsync(null);
        _sut.Days = 60;
        await _sut.LoadCommand.ExecuteAsync(null);
        gate.TrySetResult(Result(30, new AccountForecast(1, "Checking", "USD", Series(30, 1_000))));
        await first;

        _sut.View!.Values.Should().HaveCount(61);
        _sut.IsLoading.Should().BeFalse();
    }
}
