using AwesomeAssertions;
using Microsoft.Extensions.Time.Testing;
using MoneyRecord.Models;
using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;
using NSubstitute;
using Xunit;

namespace MoneyRecord.Tests.Services;

public sealed class AppLockTests
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 3, 15, 9, 0, 0, TimeSpan.Zero));
    private readonly PrivacySettings _settings = new(new FakePreferences()) { LockEnabled = true, LockAfterMinutes = 5 };
    private readonly IDeviceLock _deviceLock = Substitute.For<IDeviceLock>();
    private readonly AppLock _sut;

    public AppLockTests()
    {
        _deviceLock.IsAvailable.Returns(true);
        _sut = new AppLock(_settings, _deviceLock, _clock);
    }

    private bool AwayFor(TimeSpan time)
    {
        _sut.Backgrounded();
        _clock.Advance(time);
        return _sut.LockOnResume();
    }

    [Fact]
    public void The_app_locks_at_start_when_the_lock_is_on()
    {
        _sut.LockOnStart().Should().BeTrue();
        _sut.IsLocked.Should().BeTrue();
    }

    [Fact]
    public void Nothing_locks_when_the_lock_is_off()
    {
        _settings.LockEnabled = false;

        _sut.LockOnStart().Should().BeFalse();
        AwayFor(TimeSpan.FromHours(2)).Should().BeFalse();
    }

    [Fact]
    public void Without_a_screen_lock_on_the_phone_nothing_locks()
    {
        // Removing the phone's screen lock while the app lock is on must not lock the user out
        _deviceLock.IsAvailable.Returns(false);

        _sut.LockOnStart().Should().BeFalse();
        AwayFor(TimeSpan.FromHours(2)).Should().BeFalse();
    }

    [Fact]
    public void A_short_trip_out_of_the_app_does_not_lock_it()
    {
        // Taking a receipt photo or picking a backup file leaves the app for a moment
        _sut.LockOnStart();
        _sut.Unlock();

        AwayFor(TimeSpan.FromMinutes(4)).Should().BeFalse();
        _sut.IsLocked.Should().BeFalse();
    }

    [Fact]
    public void Coming_back_after_the_delay_locks_it()
    {
        _sut.LockOnStart();
        _sut.Unlock();

        AwayFor(TimeSpan.FromMinutes(5)).Should().BeTrue();
        _sut.IsLocked.Should().BeTrue();
    }

    [Fact]
    public void Coming_back_while_already_locked_asks_for_nothing_new()
    {
        // The phone's PIN screen leaves the app too; when it closes, the lock screen is still up
        _sut.LockOnStart();

        AwayFor(TimeSpan.FromMinutes(10)).Should().BeFalse();
        _sut.IsLocked.Should().BeTrue();
    }

    [Fact]
    public void The_time_away_counts_from_when_the_app_first_left()
    {
        _sut.LockOnStart();
        _sut.Unlock();

        _sut.Backgrounded();
        _clock.Advance(TimeSpan.FromMinutes(3));
        _sut.Backgrounded();
        _clock.Advance(TimeSpan.FromMinutes(3));

        _sut.LockOnResume().Should().BeTrue();
    }

    [Fact]
    public void Defaults_are_lock_off_after_one_minute_and_balances_shown()
    {
        var settings = new PrivacySettings(new FakePreferences());

        settings.LockEnabled.Should().BeFalse();
        settings.LockAfterMinutes.Should().Be(1);
        settings.HideBalances.Should().BeFalse();
    }
}

[Collection(LanguageCollection.Name)]
public sealed class LockDelayOptionTests : LanguageTestBase
{
    [Fact]
    public void The_choices_are_named_in_the_current_language()
    {
        LockDelayOption.All().Select(o => o.Name).Should().Equal("1 minute", "5 minutes", "15 minutes", "1 hour");
    }
}
