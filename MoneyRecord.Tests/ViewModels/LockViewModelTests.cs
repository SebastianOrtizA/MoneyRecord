using AwesomeAssertions;
using Microsoft.Extensions.Time.Testing;
using MoneyRecord.Models;
using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.ViewModels;
using NSubstitute;
using Xunit;

namespace MoneyRecord.Tests.ViewModels;

[Collection(LanguageCollection.Name)]
public sealed class LockViewModelTests : LanguageTestBase
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 3, 15, 9, 0, 0, TimeSpan.Zero));
    private readonly PrivacySettings _settings = new(new FakePreferences()) { LockEnabled = true, LockMethod = LockMethod.Pin };
    private readonly IPinStore _store = Substitute.For<IPinStore>();
    private readonly IDeviceLock _deviceLock = Substitute.For<IDeviceLock>();
    private readonly AppLock _appLock;
    private readonly PinService _pins;
    private readonly LockViewModel _sut;
    private string? _hash;
    private string? _lockout;

    public LockViewModelTests()
    {
        _store.GetHashAsync().Returns(_ => Task.FromResult(_hash));
        _store.SetHashAsync(Arg.Any<string?>()).Returns(call => { _hash = call.Arg<string?>(); return Task.CompletedTask; });
        _store.GetLockoutAsync().Returns(_ => Task.FromResult(_lockout));
        _store.SetLockoutAsync(Arg.Any<string?>()).Returns(call => { _lockout = call.Arg<string?>(); return Task.CompletedTask; });
        _appLock = new AppLock(_settings, _deviceLock, _clock);
        _pins = new PinService(_store, _settings, _clock);
        _pins.SetAsync("4821").GetAwaiter().GetResult();
        _appLock.LockOnStart();
        _sut = new LockViewModel(_appLock, _pins, _settings, _deviceLock, _clock);
    }

    private void Type(string digits)
    {
        foreach (var digit in digits)
            _sut.PressDigitCommand.Execute(digit.ToString());
    }

    [Fact]
    public async Task The_right_PIN_unlocks_the_app_and_closes_the_screen()
    {
        var closed = false;
        _sut.Unlocked += () => closed = true;
        Type("4821");

        await _sut.SubmitCommand.ExecuteAsync(null);

        _appLock.IsLocked.Should().BeFalse();
        closed.Should().BeTrue();
    }

    [Fact]
    public async Task A_wrong_PIN_clears_the_dots_and_says_how_many_tries_are_left()
    {
        Type("1111");

        await _sut.SubmitCommand.ExecuteAsync(null);

        _appLock.IsLocked.Should().BeTrue();
        _sut.Digits.Should().BeEmpty();
        _sut.Message.Should().Contain("4");
    }

    [Fact]
    public async Task Five_wrong_PINs_lock_the_pad_with_a_countdown_that_ends()
    {
        for (var i = 0; i < 5; i++)
        {
            Type("1111");
            await _sut.SubmitCommand.ExecuteAsync(null);
        }

        _sut.IsLockedOut.Should().BeTrue();
        _sut.Message.Should().Contain("0:30");
        Type("4821");
        _sut.Digits.Should().BeEmpty("the pad takes no digits while it waits");

        _clock.Advance(TimeSpan.FromSeconds(10));
        _sut.Tick();
        _sut.Message.Should().Contain("0:20");

        _clock.Advance(TimeSpan.FromSeconds(20));
        _sut.Tick();
        _sut.IsLockedOut.Should().BeFalse();
        _sut.Message.Should().BeEmpty();
        Type("4821");
        _sut.Digits.Should().Be("4821");
    }

    [Fact]
    public async Task A_lockout_still_running_shows_when_the_screen_opens_again()
    {
        for (var i = 0; i < 5; i++)
        {
            Type("1111");
            await _sut.SubmitCommand.ExecuteAsync(null);
        }

        var reopened = new LockViewModel(_appLock, _pins, _settings, _deviceLock, _clock);
        await reopened.InitializeAsync();

        reopened.IsLockedOut.Should().BeTrue();
    }

    [Fact]
    public async Task A_lost_PIN_with_no_phone_lock_opens_the_app_and_turns_the_lock_off()
    {
        _hash = null;
        _deviceLock.IsAvailable.Returns(false);
        var closed = false;
        _sut.Unlocked += () => closed = true;

        await _sut.InitializeAsync();

        closed.Should().BeTrue();
        _appLock.IsLocked.Should().BeFalse();
        _settings.LockEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task A_lost_PIN_with_a_phone_lock_falls_back_to_it()
    {
        _hash = null;
        _deviceLock.IsAvailable.Returns(true);
        _deviceLock.AuthenticateAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        await _sut.InitializeAsync();

        _sut.ShowPad.Should().BeFalse();
        _sut.CanUseDeviceLock.Should().BeTrue();
        _sut.Message.Should().NotBeEmpty();
        _appLock.IsLocked.Should().BeTrue();
    }

    [Fact]
    public async Task Too_short_a_PIN_is_not_submitted()
    {
        Type("48");

        await _sut.SubmitCommand.ExecuteAsync(null);

        _sut.Digits.Should().Be("48");
        _appLock.IsLocked.Should().BeTrue();
    }

    [Fact]
    public void The_pad_takes_at_most_8_digits_and_backspace_removes_one()
    {
        Type("123456789");

        _sut.Digits.Should().Be("12345678");
        _sut.BackspaceCommand.Execute(null);
        _sut.Digits.Should().Be("1234567");
        _sut.Dots.Should().Be("● ● ● ● ● ● ●");
    }

    [Fact]
    public async Task With_both_methods_the_phone_lock_button_unlocks_too()
    {
        _settings.LockMethod = LockMethod.DeviceLockOrPin;
        _deviceLock.IsAvailable.Returns(true);
        _deviceLock.AuthenticateAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(true);

        _sut.ShowPad.Should().BeTrue();
        _sut.CanUseDeviceLock.Should().BeTrue();
        await _sut.UseDeviceLockCommand.ExecuteAsync(null);

        _appLock.IsLocked.Should().BeFalse();
    }

    [Fact]
    public void A_PIN_only_lock_offers_no_phone_lock_button()
    {
        _deviceLock.IsAvailable.Returns(true);

        _sut.CanUseDeviceLock.Should().BeFalse();
    }
}

[Collection(LanguageCollection.Name)]
public sealed class PinSetupViewModelTests : LanguageTestBase
{
    private readonly PinService _pins;
    private readonly PrivacySettings _settings = new(new FakePreferences());
    private readonly PinSetupViewModel _sut;
    private string? _hash;

    public PinSetupViewModelTests()
    {
        var store = Substitute.For<IPinStore>();
        store.SetHashAsync(Arg.Any<string?>()).Returns(call => { _hash = call.Arg<string?>(); return Task.CompletedTask; });
        _pins = new PinService(store, _settings, TimeProvider.System);
        _sut = new PinSetupViewModel(_pins);
    }

    private async Task EnterAsync(string digits)
    {
        foreach (var digit in digits)
            _sut.PressDigitCommand.Execute(digit.ToString());
        await _sut.SubmitCommand.ExecuteAsync(null);
    }

    [Fact]
    public async Task Typing_the_same_PIN_twice_saves_it()
    {
        bool? saved = null;
        _sut.Finished += result => saved = result;

        await EnterAsync("4821");
        _sut.IsConfirming.Should().BeTrue();
        await EnterAsync("4821");

        saved.Should().BeTrue();
        _pins.HasPin.Should().BeTrue();
        _hash.Should().NotBeNull();
    }

    [Fact]
    public async Task Different_PINs_start_again_from_the_first()
    {
        await EnterAsync("4821");
        await EnterAsync("4822");

        _sut.IsConfirming.Should().BeFalse();
        _sut.Error.Should().NotBeEmpty();
        _pins.HasPin.Should().BeFalse();
    }

    [Fact]
    public async Task A_repeated_digit_PIN_is_refused_on_the_first_entry()
    {
        await EnterAsync("1111");

        _sut.IsConfirming.Should().BeFalse();
        _sut.Error.Should().NotBeEmpty();
    }

    [Fact]
    public void Cancel_finishes_without_saving()
    {
        bool? saved = null;
        _sut.Finished += result => saved = result;

        _sut.CancelCommand.Execute(null);

        saved.Should().BeFalse();
        _pins.HasPin.Should().BeFalse();
    }
}
