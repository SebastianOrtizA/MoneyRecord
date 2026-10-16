using AwesomeAssertions;
using MoneyRecord.Models;
using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.Tests.Services;
using MoneyRecord.ViewModels;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace MoneyRecord.Tests.ViewModels;

[Collection(LanguageCollection.Name)]
public sealed class AutoBackupViewModelTests : LanguageTestBase
{
    private readonly IAutoBackupService _service = Substitute.For<IAutoBackupService>();
    private readonly AutoBackupSettings _settings = new(new FakePreferences());
    private readonly FakeBackupFolder _folder = new() { Name = null };
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly IErrorHandler _errors = Substitute.For<IErrorHandler>();

    private AutoBackupViewModel CreateSut() => new(_service, _settings, _folder, _dialogs, _errors);

    [Fact]
    public async Task TurningOn_WithoutAFolder_PicksOne_ThenBacksUpAtOnce()
    {
        var sut = CreateSut();

        sut.Enabled = true;
        await sut.TurnOnCommand.ExecutionTask!;

        sut.Enabled.Should().BeTrue();
        sut.FolderText.Should().Be("Picked");
        await _service.Received(1).BackUpNowAsync();
    }

    [Fact]
    public async Task TurningOn_PickerCancelled_StaysOff()
    {
        _folder.Picks = false;
        var sut = CreateSut();

        sut.Enabled = true;
        await sut.TurnOnCommand.ExecutionTask!;

        sut.Enabled.Should().BeFalse();
        await _service.DidNotReceive().BackUpNowAsync();
    }

    [Fact]
    public async Task TurningOn_WithAFolder_BacksUpOnlyWhenDue()
    {
        _folder.Name = "Drive";
        var sut = CreateSut();

        sut.Enabled = true;
        await sut.TurnOnCommand.ExecutionTask!;

        sut.Enabled.Should().BeTrue();
        await _service.Received(1).RunIfDueAsync();
        await _service.DidNotReceive().BackUpNowAsync();
    }

    [Fact]
    public async Task Status_ShowsTheLastBackup_AFailure_OrALostFolder()
    {
        _folder.Name = "Drive";
        _settings.Enabled = true;
        var sut = CreateSut();
        sut.Status.Should().Be("No backup yet");

        _settings.LastBackup = new DateTime(2026, 10, 16, 9, 0, 0);
        await sut.RefreshCommand.ExecuteAsync(null);
        sut.Status.Should().Be("Last backup: Oct 16, 2026");

        _settings.LastFailure = new DateTime(2026, 10, 17, 9, 0, 0);
        await sut.RefreshCommand.ExecuteAsync(null);
        sut.Status.Should().Be("The last backup failed (Oct 17, 2026)");

        _folder.Name = null;
        await sut.RefreshCommand.ExecuteAsync(null);
        sut.Status.Should().Be("The app can't reach the folder any more. Choose it again.");
        sut.FolderText.Should().Be("Not chosen");
        sut.CanBackUpNow.Should().BeFalse();
    }

    [Fact]
    public async Task BackUpNow_Failure_IsShownByTheErrorHandler()
    {
        _folder.Name = "Drive";
        var failure = new IOException("disk full");
        _service.BackUpNowAsync().ThrowsAsync(failure);
        var sut = CreateSut();

        await sut.BackUpNowCommand.ExecuteAsync(null);

        await _errors.Received(1).HandleAsync(failure, "Failed to create backup");
        sut.IsBusy.Should().BeFalse();
    }

    [Fact]
    public void Pickers_ShowTheSavedChoices()
    {
        _settings.Frequency = BackupFrequency.Monthly;
        _settings.Keep = 10;

        var sut = CreateSut();

        sut.SelectedFrequency!.Name.Should().Be("Every month");
        sut.SelectedKeep!.Name.Should().Be("10 backups");
        sut.KeepOptions.Select(o => o.Count).Should().Equal(AutoBackupSettings.KeepChoices);
    }
}
