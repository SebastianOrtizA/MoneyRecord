using AwesomeAssertions;
using MoneyRecord.Models;
using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;
using NSubstitute;
using Xunit;

namespace MoneyRecord.Tests.Services;

public class GetStartedServiceTests
{
    private readonly FakePreferences _preferences = new();
    private readonly ITransactionRepository _transactions = Substitute.For<ITransactionRepository>();
    private readonly IBackupHistory _backups = Substitute.For<IBackupHistory>();
    private readonly IPrivacySettings _privacy = Substitute.For<IPrivacySettings>();
    private readonly IReminderSettings _reminders = Substitute.For<IReminderSettings>();
    private readonly IDeviceLock _deviceLock = Substitute.For<IDeviceLock>();
    private readonly GetStartedService _sut;

    public GetStartedServiceTests()
    {
        _deviceLock.IsSupported.Returns(true);
        _deviceLock.IsAvailable.Returns(true);
        _sut = new GetStartedService(_preferences, _transactions, _backups, _privacy, _reminders, _deviceLock);
    }

    [Fact]
    public async Task A_new_install_sees_the_open_checklist()
    {
        _transactions.AnyAsync().Returns(false);

        var checklist = await _sut.GetOpenAsync();

        checklist.Should().NotBeNull();
        checklist!.Rows.Should().OnlyContain(r => !r.Done);
    }

    [Fact]
    public async Task Rows_tick_as_the_user_does_things()
    {
        _transactions.AnyAsync().Returns(false);
        await _sut.GetOpenAsync();
        _transactions.AnyAsync().Returns(true);
        _backups.LastWritten.Returns(new DateTime(2026, 10, 1));
        _privacy.LockEnabled.Returns(true);

        var checklist = await _sut.GetOpenAsync();

        checklist!.Rows.Where(r => r.Done).Select(r => r.Step)
            .Should().Equal(GetStartedStep.AddEntry, GetStartedStep.Backup, GetStartedStep.AppLock);
    }

    [Fact]
    public async Task An_install_that_already_has_transactions_starts_dismissed()
    {
        _transactions.AnyAsync().Returns(true);

        (await _sut.GetOpenAsync()).Should().BeNull();

        // And stays so, whatever happens to the data later
        _transactions.AnyAsync().Returns(false);
        (await _sut.GetOpenAsync()).Should().BeNull();
    }

    [Fact]
    public async Task Hiding_it_is_for_good()
    {
        _transactions.AnyAsync().Returns(false);
        await _sut.GetOpenAsync();

        _sut.Dismiss();

        (await _sut.GetOpenAsync()).Should().BeNull();
        (await new GetStartedService(_preferences, _transactions, _backups, _privacy, _reminders, _deviceLock).GetOpenAsync())
            .Should().BeNull("the choice is saved in the preferences");
    }

    [Fact]
    public async Task Without_a_phone_lock_there_is_no_app_lock_row()
    {
        _transactions.AnyAsync().Returns(false);
        _deviceLock.IsAvailable.Returns(false);

        var checklist = await _sut.GetOpenAsync();

        checklist!.Rows.Select(r => r.Step).Should().NotContain(GetStartedStep.AppLock);
    }

    [Fact]
    public async Task Doing_everything_closes_the_checklist()
    {
        _transactions.AnyAsync().Returns(false);
        await _sut.GetOpenAsync();
        _transactions.AnyAsync().Returns(true);
        _backups.LastWritten.Returns(new DateTime(2026, 10, 1));
        _privacy.LockEnabled.Returns(true);
        _reminders.Enabled.Returns(true);

        (await _sut.GetOpenAsync()).Should().BeNull();
    }

    [Fact]
    public async Task A_finished_checklist_stays_closed_when_something_is_turned_off_again()
    {
        _transactions.AnyAsync().Returns(false);
        await _sut.GetOpenAsync();
        _transactions.AnyAsync().Returns(true);
        _backups.LastWritten.Returns(new DateTime(2026, 10, 1));
        _privacy.LockEnabled.Returns(true);
        _reminders.Enabled.Returns(true);
        await _sut.GetOpenAsync();

        _reminders.Enabled.Returns(false);

        (await _sut.GetOpenAsync()).Should().BeNull();
    }
}
