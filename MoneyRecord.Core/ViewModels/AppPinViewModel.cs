using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.ViewModels
{
    /// <summary>Settings → Privacy: the app PIN (set, change, remove) and what the lock screen accepts.</summary>
    public partial class AppPinViewModel : ObservableObject
    {
        private readonly PinService _pins;
        private readonly IPrivacySettings _privacy;
        private readonly IDeviceLock _deviceLock;
        private readonly IDialogService _dialogs;

        public AppPinViewModel(PinService pins, IPrivacySettings privacy, IDeviceLock deviceLock, IDialogService dialogs)
        {
            _pins = pins;
            _privacy = privacy;
            _deviceLock = deviceLock;
            _dialogs = dialogs;
            Refresh();
        }

        /// <summary>The app lock settings changed (the PIN went away with nothing else to open the app): Settings re-reads its switch.</summary>
        public event Action? Changed;

        /// <summary>Platforms that can keep a PIN (Android) show its row.</summary>
        public bool IsSupported => _pins.IsSupported;

        public bool HasPin => _pins.HasPin;

        public string SetPinText => HasPin ? AppResources.ChangePin : AppResources.SetPin;

        /// <summary>With a PIN and a phone lock both, the user picks which one (or either) opens the app.</summary>
        public bool ShowMethodPicker => HasPin && _deviceLock.IsAvailable;

        [ObservableProperty]
        private IReadOnlyList<LockMethodOption> methodOptions = [];

        [ObservableProperty]
        private LockMethodOption? selectedMethod;

        partial void OnSelectedMethodChanged(LockMethodOption? value)
        {
            if (value != null)
                _privacy.LockMethod = value.Method;
        }

        /// <summary>Reads the settings again (also after a language change: the names are translated).</summary>
        public void Refresh()
        {
            MethodOptions = LockMethodOption.All(_deviceLock.IsAvailable);
            SelectedMethod = MethodOptions.FirstOrDefault(o => o.Method == _privacy.LockMethod) ?? MethodOptions[0];
            OnPropertyChanged(nameof(HasPin));
            OnPropertyChanged(nameof(SetPinText));
            OnPropertyChanged(nameof(ShowMethodPicker));
        }

        /// <summary>Asks for a new PIN; true when it was saved. With no phone lock to fall back on, the PIN becomes the way in.</summary>
        public async Task<bool> SetUpAsync()
        {
            if (!await _dialogs.SetPinAsync())
                return false;
            if (!_deviceLock.IsAvailable)
                _privacy.LockMethod = LockMethod.Pin;
            Refresh();
            await _dialogs.ToastAsync(AppResources.PinSaved);
            return true;
        }

        [RelayCommand]
        private async Task SetPinAsync()
        {
            if (await SetUpAsync())
                Changed?.Invoke();
        }

        [RelayCommand]
        private async Task RemovePinAsync()
        {
            if (!await _dialogs.ConfirmAsync(AppResources.RemovePin, AppResources.RemovePinMessage, AppResources.RemovePin,
                    AppResources.Cancel))
                return;

            await _pins.RemoveAsync();
            // Nothing else can open the app: the lock goes off instead of silently doing nothing
            if (!_deviceLock.IsAvailable)
                _privacy.LockEnabled = false;
            Refresh();
            Changed?.Invoke();
            await _dialogs.ToastAsync(AppResources.PinRemoved);
        }
    }
}
