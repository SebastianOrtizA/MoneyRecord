using AwesomeAssertions;
using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.Tests.Services;
using MoneyRecord.ViewModels;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace MoneyRecord.Tests.ViewModels;

[Collection(LanguageCollection.Name)]
public sealed class BackupPasswordViewModelTests : LanguageTestBase
{
    private static readonly RestoreResult Restored = new(3, "snapshot.zip");

    private readonly FakeBackupPasswordStore _store = new();
    private readonly IDataExportService _export = Substitute.For<IDataExportService>();
    private readonly AutoBackupSettings _autoBackup = new(new FakePreferences()) { LastFingerprint = "data-1" };
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly BackupPasswordViewModel _sut;

    public BackupPasswordViewModelTests() => _sut = new BackupPasswordViewModel(_store, _export, _autoBackup, _dialogs);

    [Fact]
    public async Task TurningOn_SavesTheConfirmedPassword_AndTheNextAutomaticBackupUsesIt()
    {
        _dialogs.PasswordAsync(Arg.Any<string>(), Arg.Any<string>(), confirm: true).Returns("s3cret");

        _sut.IsProtected = true;
        await _sut.SetPasswordCommand.ExecutionTask!;

        _sut.IsProtected.Should().BeTrue();
        _store.Password.Should().Be("s3cret");
        _autoBackup.LastFingerprint.Should().BeNull("an unchanged backup is still written with the new password");
    }

    [Fact]
    public async Task TurningOn_Cancelled_StaysOff()
    {
        _dialogs.PasswordAsync(Arg.Any<string>(), Arg.Any<string>(), confirm: true).Returns((string?)null);

        _sut.IsProtected = true;
        await _sut.SetPasswordCommand.ExecutionTask!;

        _sut.IsProtected.Should().BeFalse();
        _store.Password.Should().BeNull();
        _autoBackup.LastFingerprint.Should().Be("data-1");
    }

    [Theory]
    [InlineData(true, null)]
    [InlineData(false, "s3cret")]
    public async Task TurningOff_RemovesThePasswordOnlyWhenConfirmed(bool confirmed, string? left)
    {
        _store.Password = "s3cret";
        await _sut.RefreshCommand.ExecuteAsync(null);
        _dialogs.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(confirmed);

        _sut.IsProtected = false;
        await _sut.RemovePasswordCommand.ExecutionTask!;

        _store.Password.Should().Be(left);
        _sut.IsProtected.Should().Be(!confirmed);
    }

    [Fact]
    public async Task Restore_TriesTheSavedPasswordFirst()
    {
        _store.Password = "s3cret";
        _export.RestoreAsync("b.zip", "s3cret").Returns(Restored);

        (await _sut.RestoreAsync("b.zip")).Should().Be(Restored);

        await _dialogs.DidNotReceive().PasswordAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task Restore_AsksUntilThePasswordIsRight()
    {
        _store.Password = "new";
        _export.RestoreAsync("b.zip", Arg.Is<string?>(p => p != "old")).Throws(new BackupPasswordException(wrongPassword: true));
        _export.RestoreAsync("b.zip", "old").Returns(Restored);
        _dialogs.PasswordAsync("Protected backup", Arg.Any<string>()).Returns("typo", "old");

        (await _sut.RestoreAsync("b.zip")).Should().Be(Restored);

        Received.InOrder(() =>
        {
            _dialogs.PasswordAsync("Protected backup", "Enter the password this backup was made with.");
            _dialogs.PasswordAsync("Protected backup", "Wrong password. Try again.");
        });
    }

    [Fact]
    public async Task Restore_Cancelled_ReturnsNull()
    {
        _export.RestoreAsync("b.zip", null).Throws(new BackupPasswordException(wrongPassword: false));
        _dialogs.PasswordAsync(Arg.Any<string>(), Arg.Any<string>()).Returns((string?)null);

        (await _sut.RestoreAsync("b.zip")).Should().BeNull();
    }
}
