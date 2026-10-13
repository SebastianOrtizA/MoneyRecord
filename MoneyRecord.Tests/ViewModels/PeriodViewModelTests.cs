using System.Globalization;
using AwesomeAssertions;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services;
using MoneyRecord.ViewModels;
using Xunit;

namespace MoneyRecord.Tests.ViewModels;

public class PeriodViewModelTests
{
    [Fact]
    public void RefreshNames_gives_new_items_and_keeps_the_period_without_reloading()
    {
        var sut = new PeriodViewModel();
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
}

/// <summary>The shared period follows a language change by itself (it changes global culture, so it runs alone).</summary>
[Collection(LanguageCollection.Name)]
public sealed class PeriodViewModelLanguageTests : LanguageTestBase
{
    [Fact]
    public void A_language_change_translates_the_period_names_and_keeps_the_period()
    {
        var sut = new PeriodViewModel();
        sut.SelectedPeriod = sut.Periods.First(p => p.Type == PeriodType.LastMonth);
        var oldItems = sut.Periods;

        LocalizationService.Instance.SetLanguage("es", new FakePreferences());

        sut.Periods.Should().NotBeSameAs(oldItems);
        sut.SelectedPeriod.Type.Should().Be(PeriodType.LastMonth);
        sut.SelectedPeriod.DisplayName.Should()
            .Be(AppResources.ResourceManager.GetString(nameof(AppResources.LastMonth), new CultureInfo("es")));
    }
}
