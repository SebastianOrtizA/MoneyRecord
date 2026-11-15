using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyRecord.Helpers;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.Services.Sync;

namespace MoneyRecord.ViewModels
{
    /// <summary>A device this one syncs with, as the list shows it.</summary>
    public sealed record SyncPeerRow(string DeviceId, string Name, string LastSyncText);

    /// <summary>
    /// More → Sync: turning sync on at the computer, the code it shows, connecting a phone with that code, Sync now, and the devices.
    /// </summary>
    public partial class SyncViewModel(ISyncService sync, IDialogService dialogs, IErrorHandler errors, IClipboardService clipboard) : ObservableObject
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowNotAvailable), nameof(ShowTurnOn), nameof(ShowConnect))]
        private bool isSupported = true;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowTurnOn), nameof(ShowConnect), nameof(ShowDevices), nameof(ShowAddDevice), nameof(ShowTurnOff))]
        private bool isEnabled;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowTurnOn), nameof(ShowConnect), nameof(ShowAddDevice))]
        private bool canListen;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowAddDevice))]
        private bool isListening;

        [ObservableProperty]
        private string deviceName = "";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasCode))]
        private string? codeText;

        [ObservableProperty]
        private string codeHint = "";

        [ObservableProperty]
        private bool isBusy;

        public ObservableCollection<SyncPeerRow> Peers { get; } = [];

        public bool ShowNotAvailable => !IsSupported;
        public bool ShowTurnOn => IsSupported && CanListen && !IsEnabled;
        public bool ShowConnect => IsSupported && !CanListen && !IsEnabled;
        public bool ShowDevices => IsEnabled;
        public bool ShowAddDevice => IsEnabled && IsListening;
        public bool ShowTurnOff => IsEnabled;
        public bool HasCode => !string.IsNullOrEmpty(CodeText);

        public async Task InitializeAsync()
        {
            try
            {
                var status = await sync.GetStatusAsync();
                IsSupported = status.Supported;
                IsEnabled = status.Enabled;
                CanListen = status.CanListen;
                IsListening = status.Listening;
                DeviceName = status.DeviceName;
                Peers.Clear();
                foreach (var peer in status.Peers)
                    Peers.Add(new SyncPeerRow(peer.DeviceId, peer.Name, LastSyncText(peer.LastSync)));
                if (!IsEnabled)
                    CodeText = null;
            }
            catch (Exception ex)
            {
                await errors.HandleAsync(ex, AppResources.SyncFailed);
            }
        }

        private static string LastSyncText(DateTimeOffset? at) => at is { } when
            ? string.Format(AppResources.SyncLastSync, $"{DateFormatter.Format(when.LocalDateTime, DateStyle.Medium)} {when.LocalDateTime:t}")
            : AppResources.SyncNever;

        [RelayCommand]
        private async Task TurnOnAsync()
        {
            if (IsBusy)
                return;
            IsBusy = true;
            try
            {
                await sync.EnableAsync();
                await InitializeAsync();
                await ShowCodeAsync();
            }
            catch (Exception ex)
            {
                await errors.HandleAsync(ex, AppResources.SyncFailed);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task TurnOffAsync()
        {
            if (IsBusy || !await dialogs.ConfirmAsync(AppResources.SyncTurnOffTitle, AppResources.SyncTurnOffMessage, AppResources.SyncTurnOff, AppResources.Cancel))
                return;
            IsBusy = true;
            try
            {
                await sync.DisableAsync();
                CodeText = null;
                await InitializeAsync();
            }
            catch (Exception ex)
            {
                await errors.HandleAsync(ex, AppResources.SyncFailed);
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>Shows a new one-time code (also drawn as a QR) for another device to connect with.</summary>
        [RelayCommand]
        private async Task ShowCodeAsync()
        {
            try
            {
                var code = await sync.CreatePairingCodeAsync();
                CodeText = code.Text;
                CodeHint = string.Format(AppResources.SyncCodeHint, Math.Max(1, (int)Math.Round((code.Expires - DateTimeOffset.UtcNow).TotalMinutes)));
            }
            catch (Exception ex)
            {
                await errors.HandleAsync(ex, AppResources.SyncFailed);
            }
        }

        [RelayCommand]
        private async Task CopyCodeAsync()
        {
            if (CodeText is not { Length: > 0 } code)
                return;
            await clipboard.SetTextAsync(code);
            await dialogs.ToastAsync(AppResources.SyncCodeCopied);
        }

        /// <summary>At the phone: asks for the code, confirms that this device's data is replaced, and pairs.</summary>
        [RelayCommand]
        private async Task ConnectAsync()
        {
            if (IsBusy)
                return;
            var code = await dialogs.PromptAsync(AppResources.SyncEnterCodeTitle, AppResources.SyncEnterCodeMessage, AppResources.SyncConnectButton, AppResources.Cancel);
            if (string.IsNullOrWhiteSpace(code))
                return;
            if (!await dialogs.ConfirmAsync(AppResources.SyncReplaceTitle, AppResources.SyncReplaceMessage, AppResources.SyncReplaceAccept, AppResources.Cancel))
                return;

            IsBusy = true;
            try
            {
                var outcome = await sync.PairAsync(code);
                if (outcome == SyncOutcome.Done)
                {
                    await InitializeAsync();
                    await dialogs.ToastAsync(string.Format(AppResources.SyncConnected, Peers.FirstOrDefault()?.Name ?? ""));
                }
                else
                {
                    await errors.HandleAsync(MessageOf(outcome));
                }
            }
            catch (Exception ex)
            {
                await errors.HandleAsync(ex, AppResources.SyncFailed);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task SyncNowAsync()
        {
            if (IsBusy)
                return;
            IsBusy = true;
            try
            {
                var outcome = await sync.SyncNowAsync();
                await InitializeAsync();
                if (outcome == SyncOutcome.Done)
                    await dialogs.ToastAsync(AppResources.SyncDone);
                else
                    await errors.HandleAsync(MessageOf(outcome));
            }
            catch (Exception ex)
            {
                await errors.HandleAsync(ex, AppResources.SyncFailed);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task RemovePeerAsync(SyncPeerRow peer)
        {
            await sync.RemovePeerAsync(peer.DeviceId);
            await InitializeAsync();
        }

        private static string MessageOf(SyncOutcome outcome) => outcome switch
        {
            SyncOutcome.BadCode => AppResources.SyncBadCode,
            SyncOutcome.Unreachable => AppResources.SyncUnreachable,
            SyncOutcome.WrongDevice => AppResources.SyncWrongDevice,
            SyncOutcome.NotRecognised => AppResources.SyncNotRecognised,
            SyncOutcome.VersionMismatch => AppResources.SyncVersionMismatch,
            _ => AppResources.SyncFailed
        };
    }
}
