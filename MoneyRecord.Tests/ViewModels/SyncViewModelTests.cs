using AwesomeAssertions;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.Services.Sync;
using MoneyRecord.ViewModels;
using NSubstitute;
using Xunit;

namespace MoneyRecord.Tests.ViewModels;

public sealed class SyncViewModelTests
{
    private readonly ISyncService _sync = Substitute.For<ISyncService>();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly IErrorHandler _errors = Substitute.For<IErrorHandler>();
    private readonly IClipboardService _clipboard = Substitute.For<IClipboardService>();

    private readonly IQrScanner _scanner = Substitute.For<IQrScanner>();

    private SyncViewModel Create() => new(_sync, _dialogs, _errors, _clipboard, _scanner);

    private void Status(bool enabled, bool canListen, bool listening = false, params SyncPeer[] peers) =>
        _sync.GetStatusAsync().Returns(new SyncStatus(true, enabled, canListen, listening, "My device", peers));

    [Fact]
    public async Task Computer_NotYetOn_OffersTurnOn_Phone_OffersConnect()
    {
        Status(enabled: false, canListen: true);
        var computer = Create();
        await computer.InitializeAsync();
        computer.ShowTurnOn.Should().BeTrue();
        computer.ShowConnect.Should().BeFalse();

        Status(enabled: false, canListen: false);
        var phone = Create();
        await phone.InitializeAsync();
        phone.ShowConnect.Should().BeTrue();
        phone.ShowTurnOn.Should().BeFalse();
        phone.ShowDevices.Should().BeFalse();
    }

    [Fact]
    public async Task WhenOn_ShowsDevicesWithTheirLastSync_AndTheCodeOnlyWhileListening()
    {
        Status(enabled: true, canListen: true, listening: true, new SyncPeer("p1", "Ana's phone", ["10.0.0.5"], 1, "F", null));
        var vm = Create();

        await vm.InitializeAsync();

        vm.ShowDevices.Should().BeTrue();
        vm.ShowAddDevice.Should().BeTrue();
        vm.Peers.Should().ContainSingle().Which.Should().Be(new SyncPeerRow("p1", "Ana's phone", AppResources.SyncNever));
    }

    [Fact]
    public async Task TurningOn_StartsSyncAndShowsACode()
    {
        Status(enabled: false, canListen: true);
        _sync.CreatePairingCodeAsync().Returns(new PairingCode("moneyrecord-sync:1:abc", DateTimeOffset.UtcNow.AddMinutes(5)));
        var vm = Create();
        await vm.InitializeAsync();

        await vm.TurnOnCommand.ExecuteAsync(null);

        await _sync.Received().EnableAsync();
        vm.CodeText.Should().Be("moneyrecord-sync:1:abc");
        vm.CodeHint.Should().Contain("5");
    }

    [Fact]
    public async Task Connect_AsksForTheCode_ThenConfirmsTheReplacement_BeforePairing()
    {
        Status(enabled: false, canListen: false);
        _dialogs.PromptAsync(default!, default!, default!, default!, default, default).ReturnsForAnyArgs("moneyrecord-sync:1:abc");
        _dialogs.ConfirmAsync(default!, default!, default!, default!).ReturnsForAnyArgs(false);
        var vm = Create();
        await vm.InitializeAsync();

        await vm.ConnectCommand.ExecuteAsync(null);

        await _sync.DidNotReceiveWithAnyArgs().PairAsync(default!);

        _dialogs.ConfirmAsync(default!, default!, default!, default!).ReturnsForAnyArgs(true);
        _sync.PairAsync("moneyrecord-sync:1:abc").Returns(SyncOutcome.Done);
        await vm.ConnectCommand.ExecuteAsync(null);
        await _sync.Received(1).PairAsync("moneyrecord-sync:1:abc");
    }

    [Fact]
    public async Task Scan_PairsWithTheScannedCode_AfterConfirming_AndDoesNothingWhenCancelled()
    {
        Status(enabled: false, canListen: false);
        _scanner.IsSupported.Returns(true);
        _dialogs.ConfirmAsync(default!, default!, default!, default!).ReturnsForAnyArgs(true);
        _sync.PairAsync("moneyrecord-sync:1:qr").Returns(SyncOutcome.Done);
        var vm = Create();
        await vm.InitializeAsync();
        vm.CanScan.Should().BeTrue();

        _scanner.ScanAsync().Returns((string?)null);
        await vm.ScanCodeCommand.ExecuteAsync(null);
        await _sync.DidNotReceiveWithAnyArgs().PairAsync(default!);

        _scanner.ScanAsync().Returns("moneyrecord-sync:1:qr");
        await vm.ScanCodeCommand.ExecuteAsync(null);
        await _sync.Received(1).PairAsync("moneyrecord-sync:1:qr");
    }

    [Fact]
    public async Task Scan_WhenTheScannerCannotRun_SaysSo_AndDoesNotPair()
    {
        Status(enabled: false, canListen: false);
        _scanner.ScanAsync().Returns<Task<string?>>(_ => throw new InvalidOperationException());
        var vm = Create();
        await vm.InitializeAsync();

        await vm.ScanCodeCommand.ExecuteAsync(null);

        await _errors.Received(1).HandleAsync(Arg.Any<Exception>(), AppResources.SyncScanUnavailable);
        await _sync.DidNotReceiveWithAnyArgs().PairAsync(default!);
    }

    [Theory]
    [InlineData(SyncOutcome.BadCode, nameof(AppResources.SyncBadCode))]
    [InlineData(SyncOutcome.Unreachable, nameof(AppResources.SyncUnreachable))]
    [InlineData(SyncOutcome.WrongDevice, nameof(AppResources.SyncWrongDevice))]
    [InlineData(SyncOutcome.NotRecognised, nameof(AppResources.SyncNotRecognised))]
    [InlineData(SyncOutcome.VersionMismatch, nameof(AppResources.SyncVersionMismatch))]
    public async Task AFailedPairing_ShowsAMessageForThatFailure(SyncOutcome outcome, string key)
    {
        Status(enabled: false, canListen: false);
        _dialogs.PromptAsync(default!, default!, default!, default!, default, default).ReturnsForAnyArgs("code");
        _dialogs.ConfirmAsync(default!, default!, default!, default!).ReturnsForAnyArgs(true);
        _sync.PairAsync("code").Returns(outcome);
        var vm = Create();

        await vm.ConnectCommand.ExecuteAsync(null);

        await _errors.Received().HandleAsync(typeof(AppResources).GetProperty(key)!.GetValue(null) as string);
    }

    [Fact]
    public async Task SyncNow_ToastsOnSuccess_AndExplainsAnUnreachableDevice()
    {
        Status(enabled: true, canListen: false);
        var vm = Create();
        await vm.InitializeAsync();

        _sync.SyncNowAsync().Returns(SyncOutcome.Done);
        await vm.SyncNowCommand.ExecuteAsync(null);
        await _dialogs.Received().ToastAsync(AppResources.SyncDone);

        _sync.SyncNowAsync().Returns(SyncOutcome.Unreachable);
        await vm.SyncNowCommand.ExecuteAsync(null);
        await _errors.Received().HandleAsync(AppResources.SyncUnreachable);
    }

    [Fact]
    public async Task TurningOff_AsksFirst()
    {
        Status(enabled: true, canListen: true, listening: true);
        _dialogs.ConfirmAsync(default!, default!, default!, default!).ReturnsForAnyArgs(false);
        var vm = Create();
        await vm.InitializeAsync();

        await vm.TurnOffCommand.ExecuteAsync(null);
        await _sync.DidNotReceive().DisableAsync();

        _dialogs.ConfirmAsync(default!, default!, default!, default!).ReturnsForAnyArgs(true);
        await vm.TurnOffCommand.ExecuteAsync(null);
        await _sync.Received().DisableAsync();
    }

    [Fact]
    public async Task RemovingADevice_AsksFirst()
    {
        Status(enabled: true, canListen: true, listening: true);
        var vm = Create();
        await vm.InitializeAsync();
        var row = new SyncPeerRow("p1", "Ana's phone", "");
        _dialogs.ConfirmAsync(default!, default!, default!, default!).ReturnsForAnyArgs(false);

        await vm.RemovePeerCommand.ExecuteAsync(row);
        await _sync.DidNotReceiveWithAnyArgs().RemovePeerAsync(default!);

        _dialogs.ConfirmAsync(default!, default!, default!, default!).ReturnsForAnyArgs(true);
        await vm.RemovePeerCommand.ExecuteAsync(row);
        await _sync.Received(1).RemovePeerAsync("p1");
    }
}
