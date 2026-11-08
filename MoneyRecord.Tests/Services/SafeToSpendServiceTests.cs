using AwesomeAssertions;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;
using NSubstitute;
using Xunit;

namespace MoneyRecord.Tests.Services;

public class SafeToSpendServiceTests
{
    private static readonly DateTime Today = new(2026, 11, 10);

    private static ForecastSeries Series(params long[] cents) =>
        new(cents.Select((c, i) => new ForecastPoint(Today.AddDays(i), c)).ToList());

    private static SafeToSpendService Build(ForecastResult result, SafeToSpendHorizon horizon = SafeToSpendHorizon.EndOfMonth)
    {
        var forecast = Substitute.For<IForecastService>();
        forecast.GetForecastAsync(30, Arg.Any<CancellationToken>()).Returns(result);
        var settings = Substitute.For<IPlanningSettings>();
        settings.SafeToSpendUntil.Returns(horizon);
        return new SafeToSpendService(forecast, settings);
    }

    [Fact]
    public async Task Uses_the_first_currency_and_takes_what_is_due_today_out_of_the_balance()
    {
        var due = new ForecastEvent(Today, 1, -10_000, ForecastSource.Recurring, false);
        var rent = new ForecastEvent(Today.AddDays(5), 1, -30_000, ForecastSource.Recurring, false);
        var other = new ForecastEvent(Today.AddDays(2), 2, -999_999, ForecastSource.Recurring, false);
        // Balance 100,000 now; the first point (end of today) is after the 10,000 due today
        var series = Series(90_000, 90_000, 90_000, 90_000, 90_000, 60_000);
        var result = new ForecastResult(Today, 30,
            [new AccountForecast(1, "Cash", "USD", series), new AccountForecast(2, "Pesos", "COP", Series(1, 1))],
            [new CurrencyForecast("USD", series), new CurrencyForecast("COP", Series(1, 1))], null, [], [due, rent, other]);

        var home = await Build(result).GetAsync();

        home!.CurrencyCode.Should().Be("USD");
        home.SafeToSpend.TotalCents.Should().Be(60_000);
        home.DipDate.Should().BeNull();
    }

    [Fact]
    public async Task Reports_the_first_day_the_balance_dips_below_zero()
    {
        var series = Series(5_000, 1_000, -2_000);
        var result = new ForecastResult(Today, 30, [new AccountForecast(1, "Cash", "USD", series)],
            [new CurrencyForecast("USD", series)], null, [], []);

        (await Build(result).GetAsync())!.DipDate.Should().Be(Today.AddDays(2));
    }

    [Fact]
    public async Task No_accounts_gives_nothing()
    {
        var result = new ForecastResult(Today, 30, [], [], null, [], []);

        (await Build(result).GetAsync()).Should().BeNull();
    }
}
