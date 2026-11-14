using AwesomeAssertions;
using Microsoft.Extensions.Time.Testing;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;
using NSubstitute;
using Xunit;

namespace MoneyRecord.Tests.Services;

public sealed class PinPolicyTests
{
    [Theory]
    [InlineData("1234", PinProblem.None)]
    [InlineData("12345678", PinProblem.None)]
    [InlineData("123", PinProblem.Length)]
    [InlineData("123456789", PinProblem.Length)]
    [InlineData("", PinProblem.Length)]
    [InlineData(null, PinProblem.Length)]
    [InlineData("12a4", PinProblem.NotDigits)]
    [InlineData("1 34", PinProblem.NotDigits)]
    [InlineData("0000", PinProblem.Repeated)]
    [InlineData("77777", PinProblem.Repeated)]
    public void A_PIN_is_4_to_8_digits_and_not_one_digit_repeated(string? pin, PinProblem expected) =>
        PinPolicy.Check(pin).Should().Be(expected);
}

public sealed class PinHashTests
{
    [Fact]
    public void The_right_PIN_verifies_and_a_wrong_one_does_not()
    {
        var hash = PinHash.Create("4821");

        PinHash.Verify("4821", hash).Should().BeTrue();
        PinHash.Verify("4822", hash).Should().BeFalse();
    }

    [Fact]
    public void The_hash_is_salted_and_never_holds_the_PIN()
    {
        var a = PinHash.Create("4821");
        var b = PinHash.Create("4821");

        a.Should().NotBe(b);
        a.Should().NotContain("4821");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("garbage")]
    [InlineData("v1.0.AAAA.AAAA")]
    [InlineData("v1.10.not-base64.also-not")]
    public void A_missing_or_broken_hash_verifies_nothing(string? stored) =>
        PinHash.Verify("4821", stored).Should().BeFalse();
}

public sealed class LockoutPolicyTests
{
    [Theory]
    [InlineData(1, 0)]
    [InlineData(4, 0)]
    [InlineData(5, 30)]
    [InlineData(6, 60)]
    [InlineData(7, 300)]
    [InlineData(8, 900)]
    [InlineData(9, 3600)]
    [InlineData(40, 3600)]
    public void Five_free_tries_then_growing_waits_capped_at_an_hour(int failures, int seconds) =>
        LockoutPolicy.DelayAfter(failures).Should().Be(TimeSpan.FromSeconds(seconds));

    [Theory]
    [InlineData(0, 5)]
    [InlineData(4, 1)]
    [InlineData(5, 0)]
    [InlineData(9, 0)]
    public void Free_tries_left_never_go_below_zero(int failures, int left) =>
        LockoutPolicy.FreeTriesLeft(failures).Should().Be(left);
}

public sealed class PinServiceTests
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 3, 15, 9, 0, 0, TimeSpan.Zero));
    private readonly MemoryPinStore _store = new();
    private readonly PrivacySettings _settings = new(new FakePreferences());
    private readonly PinService _sut;

    public PinServiceTests() => _sut = new PinService(_store, _settings, _clock);

    [Fact]
    public async Task Setting_a_PIN_keeps_only_its_hash_and_flags_it()
    {
        await _sut.SetAsync("4821");

        _sut.HasPin.Should().BeTrue();
        _store.Hash.Should().NotBeNull().And.NotContain("4821");
        (await _sut.VerifyAsync("4821")).Result.Should().Be(PinResult.Correct);
    }

    [Fact]
    public async Task A_PIN_that_breaks_the_rules_is_refused()
    {
        var act = () => _sut.SetAsync("1111");

        await act.Should().ThrowAsync<ArgumentException>();
        _sut.HasPin.Should().BeFalse();
    }

    [Fact]
    public async Task Wrong_tries_count_down_the_free_ones_then_lock()
    {
        await _sut.SetAsync("4821");

        var first = await _sut.VerifyAsync("0000");
        first.Should().Be(new PinCheck(PinResult.Wrong, 4));
        for (var i = 0; i < 3; i++)
            await _sut.VerifyAsync("0000");

        var fifth = await _sut.VerifyAsync("0000");
        fifth.Result.Should().Be(PinResult.Wrong);
        fifth.LockedUntil.Should().Be(_clock.GetUtcNow().AddSeconds(30));
    }

    [Fact]
    public async Task While_locked_even_the_right_PIN_is_refused_until_the_wait_is_over()
    {
        await _sut.SetAsync("4821");
        for (var i = 0; i < 5; i++)
            await _sut.VerifyAsync("0000");

        (await _sut.VerifyAsync("4821")).Result.Should().Be(PinResult.LockedOut);
        (await _sut.LockedUntilAsync()).Should().NotBeNull();

        _clock.Advance(TimeSpan.FromSeconds(30));

        (await _sut.LockedUntilAsync()).Should().BeNull();
        (await _sut.VerifyAsync("4821")).Result.Should().Be(PinResult.Correct);
    }

    [Fact]
    public async Task A_wrong_try_after_a_wait_waits_longer()
    {
        await _sut.SetAsync("4821");
        for (var i = 0; i < 5; i++)
            await _sut.VerifyAsync("0000");
        _clock.Advance(TimeSpan.FromSeconds(30));

        var sixth = await _sut.VerifyAsync("0000");

        sixth.LockedUntil.Should().Be(_clock.GetUtcNow().AddMinutes(1));
    }

    [Fact]
    public async Task The_lockout_survives_a_restart_because_it_lives_in_the_store()
    {
        await _sut.SetAsync("4821");
        for (var i = 0; i < 5; i++)
            await _sut.VerifyAsync("0000");

        var restarted = new PinService(_store, new PrivacySettings(new FakePreferences()), _clock);

        (await restarted.LockedUntilAsync()).Should().NotBeNull();
    }

    [Fact]
    public async Task A_right_PIN_resets_the_count()
    {
        await _sut.SetAsync("4821");
        for (var i = 0; i < 3; i++)
            await _sut.VerifyAsync("0000");
        await _sut.VerifyAsync("4821");

        (await _sut.VerifyAsync("0000")).TriesLeft.Should().Be(4);
    }

    [Fact]
    public async Task Removing_the_PIN_clears_everything_and_leaves_a_PIN_only_lock_on_the_phone_lock()
    {
        await _sut.SetAsync("4821");
        _settings.LockMethod = LockMethod.Pin;
        await _sut.VerifyAsync("0000");

        await _sut.RemoveAsync();

        _sut.HasPin.Should().BeFalse();
        _store.Hash.Should().BeNull();
        _store.Lockout.Should().BeNull();
        _settings.LockMethod.Should().Be(LockMethod.DeviceLock);
    }

    [Fact]
    public async Task A_PIN_whose_hash_is_gone_is_dropped_instead_of_locking_the_user_out()
    {
        await _sut.SetAsync("4821");
        await _store.SetHashAsync(null);
        _settings.LockMethod = LockMethod.Pin;

        (await _sut.VerifyAsync("4821")).Result.Should().Be(PinResult.Lost);

        _sut.HasPin.Should().BeFalse();
        _settings.LockMethod.Should().Be(LockMethod.DeviceLock);
    }

    [Fact]
    public async Task Without_a_PIN_set_there_is_nothing_to_lose()
    {
        (await _sut.EnsureHashAsync()).Should().BeTrue();
    }

    private sealed class MemoryPinStore : IPinStore
    {
        public string? Hash { get; private set; }
        public string? Lockout { get; private set; }
        public bool IsSupported => true;
        public Task<string?> GetHashAsync() => Task.FromResult(Hash);
        public Task SetHashAsync(string? hash) { Hash = hash; return Task.CompletedTask; }
        public Task<string?> GetLockoutAsync() => Task.FromResult(Lockout);
        public Task SetLockoutAsync(string? state) { Lockout = state; return Task.CompletedTask; }
    }
}

public sealed class PinLockTests
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 3, 15, 9, 0, 0, TimeSpan.Zero));
    private readonly PrivacySettings _settings = new(new FakePreferences()) { LockEnabled = true };
    private readonly IDeviceLock _deviceLock = Substitute.For<IDeviceLock>();

    private AppLock Lock() => new(_settings, _deviceLock, _clock);

    [Fact]
    public void A_PIN_only_lock_works_on_a_phone_without_a_screen_lock()
    {
        _deviceLock.IsAvailable.Returns(false);
        _settings.LockMethod = LockMethod.Pin;
        _settings.HasPin = true;

        Lock().LockOnStart().Should().BeTrue();
    }

    [Fact]
    public void A_PIN_only_lock_without_a_PIN_set_does_not_lock()
    {
        _deviceLock.IsAvailable.Returns(true);
        _settings.LockMethod = LockMethod.Pin;

        Lock().LockOnStart().Should().BeFalse();
    }

    [Fact]
    public void Either_locks_with_the_PIN_alone_or_the_phone_lock_alone()
    {
        _settings.LockMethod = LockMethod.DeviceLockOrPin;

        _deviceLock.IsAvailable.Returns(false);
        _settings.HasPin = true;
        Lock().CanLock.Should().BeTrue();

        _deviceLock.IsAvailable.Returns(true);
        _settings.HasPin = false;
        Lock().CanLock.Should().BeTrue();

        _deviceLock.IsAvailable.Returns(false);
        Lock().CanLock.Should().BeFalse();
    }

    [Fact]
    public void The_phone_lock_method_ignores_a_PIN()
    {
        _deviceLock.IsAvailable.Returns(false);
        _settings.HasPin = true;

        Lock().CanLock.Should().BeFalse();
    }
}
