using AwesomeAssertions;
using MoneyRecord.Services;
using Xunit;

namespace MoneyRecord.Tests.Services;

public class SwipeHintSettingsTests
{
    private readonly FakePreferences _preferences = new();

    [Fact]
    public void The_caption_is_due_for_three_visits()
    {
        var sut = new SwipeHintSettings(_preferences);

        for (var visit = 0; visit < SwipeHintSettings.MaxVisits; visit++)
        {
            sut.ShouldShow.Should().BeTrue();
            sut.RecordVisit();
        }

        sut.ShouldShow.Should().BeFalse();
    }

    [Fact]
    public void A_swipe_ends_it_at_once_and_for_good()
    {
        var sut = new SwipeHintSettings(_preferences);

        sut.MarkSeen();

        sut.ShouldShow.Should().BeFalse();
        new SwipeHintSettings(_preferences).ShouldShow.Should().BeFalse();
    }
}
