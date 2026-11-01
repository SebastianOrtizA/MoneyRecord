using AwesomeAssertions;
using MoneyRecord.Helpers;
using Xunit;

namespace MoneyRecord.Tests.Helpers;

public class EntryDatePresetsTests
{
    private static readonly DateTime Now = new(2026, 3, 1, 23, 30, 0);

    [Fact]
    public void Today_and_yesterday_are_dates_without_time()
    {
        EntryDatePresets.Today(Now).Should().Be(new DateTime(2026, 3, 1));
        EntryDatePresets.Yesterday(Now).Should().Be(new DateTime(2026, 2, 28));
    }

    [Theory]
    [InlineData(2026, 3, 1, EntryDatePreset.Today)]
    [InlineData(2026, 2, 28, EntryDatePreset.Yesterday)]
    [InlineData(2026, 3, 2, EntryDatePreset.Other)]
    [InlineData(2026, 2, 27, EntryDatePreset.Other)]
    public void Classify_matches_the_chip(int y, int m, int d, EntryDatePreset expected) =>
        EntryDatePresets.Classify(new DateTime(y, m, d, 8, 0, 0), Now).Should().Be(expected);
}
