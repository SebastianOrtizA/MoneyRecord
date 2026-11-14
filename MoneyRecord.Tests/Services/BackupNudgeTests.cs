using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.ViewModels;
using NSubstitute;
using Xunit;

namespace MoneyRecord.Tests.Services;

[Collection(LanguageCollection.Name)]
public sealed class BackupNudgeTests : LanguageTestBase
{
    private readonly FakeBackupPasswordStore _store = new();
    private readonly PrivacySettings _privacy = new(new FakePreferences());
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly BackupNudge _sut;

    public BackupNudgeTests()
    {
        var prompt = new BackupPasswordPrompt(_store, new AutoBackupSettings(new FakePreferences()), _dialogs);
        _sut = new BackupNudge(_store, _privacy, _dialogs, prompt, NullLogger<BackupNudge>.Instance);
    }

    private void Answer(bool accept) =>
        _dialogs.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(accept);

    [Fact]
    public async Task Accepting_AsksForThePassword_AndSavesIt()
    {
        Answer(true);
        _dialogs.PasswordAsync(Arg.Any<string>(), Arg.Any<string>(), confirm: true).Returns("s3cret");

        await _sut.OfferAsync();

        _store.Password.Should().Be("s3cret");
        _privacy.BackupNudgeShown.Should().BeTrue();
    }

    [Fact]
    public async Task Declining_IsRemembered_AndNeverAsksAgain()
    {
        Answer(false);

        await _sut.OfferAsync();
        await _sut.OfferAsync();

        await _dialogs.Received(1).ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
        _store.Password.Should().BeNull();
    }

    [Fact]
    public async Task WithoutAPasswordStore_DoesNotAskOrMarkItShown()
    {
        var store = Substitute.For<IBackupPasswordStore>();
        var sut = new BackupNudge(store, _privacy, _dialogs, Substitute.For<IBackupPasswordPrompt>(), NullLogger<BackupNudge>.Instance);

        await sut.OfferAsync();

        await _dialogs.DidNotReceive().ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
        _privacy.BackupNudgeShown.Should().BeFalse();
    }

    [Fact]
    public async Task AlreadyProtected_DoesNotAsk()
    {
        _store.Password = "abc";

        await _sut.OfferAsync();

        await _dialogs.DidNotReceive().ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }
}
