using AwesomeAssertions;
using MoneyRecord.Services;
using Xunit;

namespace MoneyRecord.Tests.Services;

public sealed class WhatsNewTrackerTests
{
    private readonly FakePreferences _preferences = new();
    private readonly WhatsNewTracker _sut;

    public WhatsNewTrackerTests()
    {
        _sut = new WhatsNewTracker(_preferences);
    }

    [Fact]
    public void An_install_that_never_recorded_a_version_is_an_update_and_sees_the_notes()
    {
        _sut.IsUnseen("0.14.0").Should().BeTrue();
    }

    [Fact]
    public void Once_shown_the_notes_of_a_version_stay_seen()
    {
        _sut.MarkSeen("0.14.0");

        _sut.IsUnseen("0.14.0").Should().BeFalse();
    }

    [Fact]
    public void The_next_version_has_notes_to_show_again()
    {
        _sut.MarkSeen("0.14.0");

        _sut.IsUnseen("0.14.1").Should().BeTrue();
        _sut.IsUnseen("0.14.0").Should().BeFalse();
    }

    [Fact]
    public void A_new_install_that_records_its_version_at_the_start_sees_nothing()
    {
        // What App does while the first-run setup is pending
        _sut.MarkSeen("0.14.0");

        new WhatsNewTracker(_preferences).IsUnseen("0.14.0").Should().BeFalse();
    }
}
