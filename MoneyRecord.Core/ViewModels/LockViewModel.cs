using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.ViewModels
{
    /// <summary>
    /// The lock screen: the PIN pad (with its lockout countdown) and the phone's lock, by <see cref="IPrivacySettings.LockMethod"/>.
    /// The page calls <see cref="Tick"/> every second while it is shown, and closes itself on <see cref="Unlocked"/>.
    /// </summary>
    public partial class LockViewModel(
        AppLock appLock,
        PinService pins,
        IPrivacySettings settings,
        IDeviceLock deviceLock,
        TimeProvider clock) : PinEntryViewModel
    {
        private DateTimeOffset? _lockedUntil;
        private bool _asking;
        private bool _checking;

        /// <summary>The app is unlocked: the page closes.</summary>
        public event Action? Unlocked;

        /// <summary>The PIN pad is shown (the method includes the PIN, and one is set).</summary>
        public bool ShowPad => settings.LockMethod != LockMethod.DeviceLock && pins.HasPin;

        /// <summary>The "Use phone lock" button: the phone has a lock the method allows.</summary>
        public bool CanUseDeviceLock => settings.LockMethod != LockMethod.Pin && deviceLock.IsAvailable;

        /// <summary>The pad shows the wait instead of taking digits.</summary>
        public bool IsLockedOut => _lockedUntil != null;

        public override bool IsEntryEnabled => !IsLockedOut && !_checking;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasMessage))]
        private string message = string.Empty;

        public bool HasMessage => Message.Length > 0;

        public string Subtitle => ShowPad ? AppResources.PinSubtitle : AppResources.UnlockSubtitle;

        /// <summary>Shows the lock screen: resumes a lockout that is still running, and asks for the phone's lock when it is the way in.</summary>
        public async Task InitializeAsync()
        {
            var lost = !await pins.EnsureHashAsync();
            if (lost)
                PinLost();
            if (ShowPad)
                SetLockedUntil(await pins.LockedUntilAsync());
            if (CanUseDeviceLock)
                await UseDeviceLockAsync();
            else if (!ShowPad)
                // Nothing left to ask for (the phone's screen lock was removed meanwhile; AppLock stops locking too)
                Unlock();
        }

        [RelayCommand]
        private async Task UseDeviceLockAsync()
        {
            if (_asking)
                return;
            _asking = true;
            try
            {
                if (await deviceLock.AuthenticateAsync(AppResources.AppLocked, AppResources.UnlockSubtitle))
                    Unlock();
            }
            finally
            {
                _asking = false;
            }
        }

        [RelayCommand]
        private async Task SubmitAsync()
        {
            if (!CanSubmit)
                return;
            _checking = true;
            NotifyEntryChanged();
            try
            {
                var check = await pins.VerifyAsync(Digits);
                ClearDigits();
                switch (check.Result)
                {
                    case PinResult.Correct:
                        Unlock();
                        return;
                    case PinResult.Lost:
                        PinLost();
                        if (!ShowPad && !CanUseDeviceLock)
                            Unlock();
                        return;
                    case PinResult.LockedOut:
                        SetLockedUntil(check.LockedUntil);
                        return;
                    default:
                        if (check.LockedUntil != null)
                            SetLockedUntil(check.LockedUntil);
                        else
                            Message = string.Format(AppResources.PinWrongTries, check.TriesLeft);
                        return;
                }
            }
            finally
            {
                _checking = false;
                NotifyEntryChanged();
            }
        }

        /// <summary>Called every second by the page: counts the wait down and opens the pad when it is over.</summary>
        public void Tick()
        {
            if (_lockedUntil != null)
                SetLockedUntil(_lockedUntil > clock.GetUtcNow() ? _lockedUntil : null);
        }

        private void SetLockedUntil(DateTimeOffset? until)
        {
            var was = IsLockedOut;
            _lockedUntil = until > clock.GetUtcNow() ? until : null;
            Message = _lockedUntil is { } end ? string.Format(AppResources.PinLockedOut, FormatWait(end - clock.GetUtcNow())) : string.Empty;
            if (was != IsLockedOut)
                NotifyEntryChanged();
        }

        /// <summary>The PIN was dropped (see <see cref="PinService.EnsureHashAsync"/>): back to the phone lock, or no lock at all.</summary>
        private void PinLost()
        {
            if (!deviceLock.IsAvailable)
                settings.LockEnabled = false;
            OnPropertyChanged(nameof(ShowPad));
            OnPropertyChanged(nameof(CanUseDeviceLock));
            OnPropertyChanged(nameof(Subtitle));
            Message = AppResources.PinLost;
        }

        private void NotifyEntryChanged()
        {
            OnPropertyChanged(nameof(IsLockedOut));
            OnPropertyChanged(nameof(IsEntryEnabled));
            OnPropertyChanged(nameof(CanSubmit));
        }

        private void Unlock()
        {
            appLock.Unlock();
            Unlocked?.Invoke();
        }

        /// <summary>"0:42", "14:03", "1:00:00": whole seconds, rounded up so the last second shows 0:01.</summary>
        internal static string FormatWait(TimeSpan wait)
        {
            var seconds = (int)Math.Ceiling(Math.Max(0, wait.TotalSeconds));
            var time = TimeSpan.FromSeconds(seconds);
            return time.TotalHours >= 1 ? $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}" : $"{time.Minutes}:{time.Seconds:00}";
        }
    }
}
