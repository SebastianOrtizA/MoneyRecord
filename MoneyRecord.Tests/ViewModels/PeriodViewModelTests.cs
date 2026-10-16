using System.Globalization;
using AwesomeAssertions;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.ViewModels;
using NSubstitute;
using Xunit;

namespace MoneyRecord.Tests.ViewModels;

public class PeriodViewModelTests
{
    [Fact]
    public void RefreshNames_gives_new_items_and_keeps_the_period_without_reloading()
    {
        var sut = new PeriodViewModel(Substitute.For<IConversionService>());
        sut.SelectedPeriod = sut.Periods.First(p => p.Type == PeriodType.LastMonth);
        var oldItems = sut.Periods;
        var changes = 0;
        using var watch = sut.WatchChanges(() => changes++);

        sut.RefreshNames();

        sut.Periods.Should().NotBeSameAs(oldItems, "a new list makes the picker read the names again");
        sut.SelectedPeriod.Type.Should().Be(PeriodType.LastMonth);
        sut.Periods.Should().Contain(sut.SelectedPeriod);
        changes.Should().Be(0);
    }

    [Fact]
    public void A_currency_chip_switches_every_report_and_reloads_only_the_reports()
    {
        var sut = new PeriodViewModel(Substitute.For<IConversionService>());
        var baseCode = MoneyFormatter.BaseCurrency;
        sut.SetCurrencies([baseCode, "ZZA"], ConversionState.Off);
        var (periodChanges, monthChanges, currencyChanges) = (0, 0, 0);
        using var periodWatch = sut.WatchChanges(() => periodChanges++);
        using var monthWatch = sut.WatchMonthCount(() => monthChanges++);
        using var currencyWatch = sut.WatchCurrency(() => currencyChanges++);

        sut.SelectCurrencyCommand.Execute(sut.CurrencyChoices.Single(c => c.Code == "ZZA"));

        sut.ReportCurrency.Should().Be("ZZA");
        sut.CurrencyChoices.Should().ContainSingle(c => c.IsSelected).Which.Code.Should().Be("ZZA");
        (periodChanges, monthChanges, currencyChanges).Should().Be((0, 0, 1), "Home, Transactions and Budgets don't show it");
    }

    [Fact]
    public void One_currency_shows_no_chips_and_a_gone_currency_falls_back_to_the_base()
    {
        var sut = new PeriodViewModel(Substitute.For<IConversionService>());
        var baseCode = MoneyFormatter.BaseCurrency;
        sut.SetCurrencies([baseCode, "ZZA"], ConversionState.Off);
        sut.SelectCurrencyCommand.Execute(sut.CurrencyChoices.Single(c => c.Code == "ZZA"));

        sut.SetCurrencies([baseCode], ConversionState.Off);

        sut.ReportCurrency.Should().Be(baseCode);
        sut.HasCurrencyChoices.Should().BeFalse();
    }

    [Fact]
    public void The_converted_chip_comes_first_when_rates_exist_and_is_remembered_for_Home()
    {
        var conversion = Substitute.For<IConversionService>();
        var sut = new PeriodViewModel(conversion);
        var baseCode = MoneyFormatter.BaseCurrency;

        sut.SetCurrencies([baseCode, "ZZA"], new ConversionState(CanConvert: true, IsConverted: false));
        sut.CurrencyChoices.Select(c => c.Label).Should().Equal($"≈ {baseCode}", baseCode, "ZZA");

        sut.SelectCurrencyCommand.Execute(sut.CurrencyChoices[0]);
        (sut.IsConverted, sut.ReportCurrency).Should().Be((true, baseCode));
        conversion.Received(1).SetConverted(true);
        sut.CurrencyChoices.Should().ContainSingle(c => c.IsSelected).Which.IsConverted.Should().BeTrue();

        sut.SelectCurrencyCommand.Execute(sut.CurrencyChoices.Single(c => c.Code == "ZZA"));
        (sut.IsConverted, sut.ReportCurrency).Should().Be((false, "ZZA"));
        conversion.Received(1).SetConverted(false);
    }

    [Fact]
    public void Without_rates_there_is_no_converted_chip()
    {
        var sut = new PeriodViewModel(Substitute.For<IConversionService>());

        sut.SetCurrencies([MoneyFormatter.BaseCurrency, "ZZA"], ConversionState.Off);

        sut.IsConverted.Should().BeFalse();
        sut.CurrencyChoices.Should().NotContain(c => c.IsConverted);
    }
}

/// <summary>The shared period follows a language change by itself (it changes global culture, so it runs alone).</summary>
[Collection(LanguageCollection.Name)]
public sealed class PeriodViewModelLanguageTests : LanguageTestBase
{
    [Fact]
    public void A_language_change_translates_the_period_names_and_keeps_the_period()
    {
        var sut = new PeriodViewModel(Substitute.For<IConversionService>());
        sut.SelectedPeriod = sut.Periods.First(p => p.Type == PeriodType.LastMonth);
        var oldItems = sut.Periods;

        LocalizationService.Instance.SetLanguage("es", new FakePreferences());

        sut.Periods.Should().NotBeSameAs(oldItems);
        sut.SelectedPeriod.Type.Should().Be(PeriodType.LastMonth);
        sut.SelectedPeriod.DisplayName.Should()
            .Be(AppResources.ResourceManager.GetString(nameof(AppResources.LastMonth), new CultureInfo("es")));
    }
}
