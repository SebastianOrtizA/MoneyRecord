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

    // 2026-10-31 12:00
    private static PeriodViewModel Stepper() => new(Substitute.For<IConversionService>(),
        new Microsoft.Extensions.Time.Testing.FakeTimeProvider(new DateTimeOffset(2026, 10, 31, 12, 0, 0, TimeSpan.Zero)));

    [Fact]
    public void Previous_shows_last_month_as_a_custom_period_and_reloads_once()
    {
        var sut = Stepper();
        var changes = 0;
        using var watch = sut.WatchChanges(() => changes++);

        sut.PreviousCommand.Execute(null);

        sut.SelectedPeriod.Type.Should().Be(PeriodType.CustomPeriod);
        sut.Range.Start.Should().Be(new DateTime(2026, 9, 1));
        sut.Range.End.Date.Should().Be(new DateTime(2026, 9, 30));
        sut.Label.Should().Be(DateFormatter.FormatMonthYear(new DateTime(2026, 9, 1)));
        changes.Should().Be(1);
    }

    [Fact]
    public void Next_back_onto_this_month_selects_this_month_again()
    {
        var sut = Stepper();
        sut.PreviousCommand.Execute(null);

        sut.NextCommand.Execute(null);

        sut.SelectedPeriod.Type.Should().Be(PeriodType.CalendarMonth);
        sut.CanStepForward.Should().BeFalse("never past the current month");
    }

    [Fact]
    public void Forward_stops_at_the_current_month_and_year()
    {
        var sut = Stepper();
        var changes = 0;
        using var watch = sut.WatchChanges(() => changes++);

        sut.NextCommand.Execute(null);
        sut.SelectedPeriod = sut.Periods.First(p => p.Type == PeriodType.CalendarYear);
        changes = 0;
        sut.NextCommand.Execute(null);

        (sut.SelectedPeriod.Type, changes).Should().Be((PeriodType.CalendarYear, 0));
        sut.CanStepForward.Should().BeFalse();
    }

    [Fact]
    public void A_year_steps_by_year()
    {
        var sut = Stepper();
        sut.SelectedPeriod = sut.Periods.First(p => p.Type == PeriodType.CalendarYear);

        sut.PreviousCommand.Execute(null);

        sut.Range.Start.Should().Be(new DateTime(2025, 1, 1));
        sut.Label.Should().Be("2025");
        sut.CanStepForward.Should().BeTrue();
    }

    [Theory]
    [InlineData(PeriodType.Today)]
    [InlineData(PeriodType.LastWeek)]
    [InlineData(PeriodType.LastMonth)]
    [InlineData(PeriodType.LastYear)]
    public void Rolling_periods_have_no_arrows_and_show_their_name(PeriodType type)
    {
        var sut = Stepper();
        sut.SelectedPeriod = sut.Periods.First(p => p.Type == type);

        sut.CanStep.Should().BeFalse();
        sut.Label.Should().Be(sut.SelectedPeriod.DisplayName);
    }

    [Fact]
    public void An_arbitrary_custom_range_shows_its_dates_and_has_no_arrows()
    {
        var sut = Stepper();
        sut.CustomStartDate = new DateTime(2026, 3, 5);
        sut.CustomEndDate = new DateTime(2026, 4, 20);
        sut.SelectedPeriod = sut.Periods.First(p => p.Type == PeriodType.CustomPeriod);

        sut.CanStep.Should().BeFalse();
        sut.Label.Should().Be(DateFormatter.FormatRange(new DateTime(2026, 3, 5), new DateTime(2026, 4, 20)));
    }

    [Fact]
    public void The_sheet_closes_on_a_period_but_stays_open_for_custom_dates()
    {
        var sut = Stepper();
        sut.OpenSheetCommand.Execute(null);

        sut.SelectPeriodCommand.Execute(sut.Periods.First(p => p.Type == PeriodType.CustomPeriod));
        sut.IsSheetOpen.Should().BeTrue();

        sut.SelectPeriodCommand.Execute(sut.Periods.First(p => p.Type == PeriodType.Today));
        sut.IsSheetOpen.Should().BeFalse();
        sut.TryCloseSheet().Should().BeFalse();
    }

    [Fact]
    public void Back_closes_an_open_sheet_once()
    {
        var sut = Stepper();
        sut.OpenSheetCommand.Execute(null);

        sut.TryCloseSheet().Should().BeTrue();
        sut.IsSheetOpen.Should().BeFalse();
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

    [Fact]
    public void The_stepper_label_follows_the_language()
    {
        var sut = new PeriodViewModel(Substitute.For<IConversionService>(),
            new Microsoft.Extensions.Time.Testing.FakeTimeProvider(new DateTimeOffset(2026, 10, 31, 12, 0, 0, TimeSpan.Zero)));
        var changed = new List<string?>();
        sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        LocalizationService.Instance.SetLanguage("es", new FakePreferences());

        sut.Label.Should().StartWith("Octubre").And.Contain("2026");
        changed.Should().Contain(nameof(PeriodViewModel.Label));
    }
}
