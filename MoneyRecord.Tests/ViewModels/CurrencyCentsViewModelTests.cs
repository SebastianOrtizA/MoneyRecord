using AwesomeAssertions;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.ViewModels;
using NSubstitute;
using Xunit;
using static MoneyRecord.Tests.TestCulture;

namespace MoneyRecord.Tests.ViewModels;

/// <summary>Settings → Show cents, with COP as the base currency (the base and the cents choices are restored afterwards).</summary>
public sealed class CurrencyCentsViewModelTests : IDisposable
{
    private readonly string _previousBase = MoneyFormatter.BaseCurrencyCode;
    private readonly FakePreferences _preferences = new();
    private readonly CurrencyService _currencies;
    private readonly IAccountRepository _accounts = Substitute.For<IAccountRepository>();
    private readonly CurrencyCentsViewModel _sut;

    public CurrencyCentsViewModelTests()
    {
        _preferences.Set(CurrencyService.BaseCurrencyPreferenceKey, "COP");
        CurrencyService.LoadSettings(_preferences);
        _currencies = new CurrencyService(_preferences);
        _sut = new CurrencyCentsViewModel(_accounts, _currencies);
    }

    public void Dispose()
    {
        CurrencyService.LoadSettings(new FakePreferences());
        MoneyFormatter.BaseCurrencyCode = _previousBase;
    }

    [Fact]
    public async Task Lists_the_base_then_each_account_currency_that_has_cents()
    {
        _accounts.GetAllAsync().Returns([new Account { CurrencyCode = "JPY" }, new Account { CurrencyCode = "USD" },
            new Account { CurrencyCode = "" }, new Account { CurrencyCode = "EUR" }]);

        await _sut.RefreshCommand.ExecuteAsync(null);

        _sut.CurrencyOptions.Select(o => o.Code).Should().Equal("COP", "EUR", "USD");
        _sut.CurrencyOptions.Should().OnlyContain(o => o.ShowsCents);
    }

    [Fact]
    public async Task Turning_cents_off_rounds_that_currency_only_and_is_saved()
    {
        _accounts.GetAllAsync().Returns([new Account { CurrencyCode = "USD" }]);
        await _sut.RefreshCommand.ExecuteAsync(null);
        var cop = _sut.CurrencyOptions[0];

        cop.ShowsCents = false;

        WithCulture("es-CO", () =>
        {
            cop.Sample.Should().Be("$1.235");
            MoneyFormatter.Format(1234.56m, "USD").Should().Be("$1.234,56 USD");
        });
        _preferences.Get(CurrencyService.HiddenCentsPreferenceKey, string.Empty).Should().Be("COP");

        // The next start reads it back
        CurrencyService.LoadSettings(_preferences);
        _currencies.ShowsCents("cop").Should().BeFalse();
        _currencies.ShowsCents("USD").Should().BeTrue();
    }

    [Fact]
    public void Hidden_cents_still_reach_the_entry_and_the_parser()
    {
        _currencies.SetShowsCents("COP", false);
        var culture = new System.Globalization.CultureInfo("es-CO");

        MoneyFormatter.FormatBesideCode(1250.50m, "COP", culture).Should().Be("$1.251");
        MoneyFormatter.FormatForEntry(1250.50m, "COP", culture).Should().Be("$1.250,50");
        MoneyFormatter.FormatForEntry(1250m, "COP", culture).Should().Be("$1.250");
        WithCulture("es-CO", () => AmountParser.Parse("$1.250,50", "COP").Should().Be(1250.50m));
    }
}
