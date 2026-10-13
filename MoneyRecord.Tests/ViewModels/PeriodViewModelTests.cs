using AwesomeAssertions;
using MoneyRecord.Models;
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
