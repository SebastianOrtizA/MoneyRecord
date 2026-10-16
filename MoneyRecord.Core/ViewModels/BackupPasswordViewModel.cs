using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.ViewModels
{
    /// <summary>
    /// Settings → Backup &amp; Restore: the backup password (switch + Change), and restoring a backup that needs one.
    /// </summary>
    public partial class BackupPasswordViewModel(
        IBackupPasswordStore store,
        IDataExportService export,
        IAutoBackupSettings autoBackup,
        IDialogService dialogs) : ObservableObject
    {
        private bool _isProtected;

        /// <summary>Platforms with secure storage (Android) show the switch.</summary>
        public bool IsSupported => store.IsSupported;

        /// <summary>The saved password for a new backup; null = a plain backup.</summary>
        public Task<string?> GetPasswordAsync() => store.GetAsync();

        /// <summary>Backups are encrypted. Turning it on asks for a password; off asks to confirm.</summary>
        public bool IsProtected
        {
            get => _isProtected;
            set
            {
                // The value is not stored here: the command asks first and then raises PropertyChanged, which puts the
                // switch back when the user cancels
                if (value == _isProtected || SetPasswordCommand.IsRunning || RemovePasswordCommand.IsRunning)
                    return;
                if (value)
                    SetPasswordCommand.Execute(null);
                else
                    RemovePasswordCommand.Execute(null);
            }
        }

        [RelayCommand]
        private async Task RefreshAsync()
        {
            if (!IsSupported)
                return;
            _isProtected = await store.GetAsync() != null;
            OnPropertyChanged(nameof(IsProtected));
        }

        /// <summary>Sets a new password (also Change); the switch stays as it was when cancelled.</summary>
        [RelayCommand]
        private async Task SetPasswordAsync()
        {
            var password = await dialogs.PasswordAsync(AppResources.BackupPasswordTitle, AppResources.BackupPasswordMessage,
                confirm: true);
            if (!string.IsNullOrEmpty(password))
            {
                await store.SetAsync(password);
                _isProtected = true;
                // The next automatic backup is made with it, even if the data didn't change
                autoBackup.LastFingerprint = null;
                await dialogs.ToastAsync(AppResources.BackupPasswordSaved);
            }
            OnPropertyChanged(nameof(IsProtected));
        }

        [RelayCommand]
        private async Task RemovePasswordAsync()
        {
            if (await dialogs.ConfirmAsync(AppResources.RemoveBackupPasswordTitle, AppResources.RemoveBackupPasswordMessage,
                    AppResources.RemovePassword, AppResources.Cancel))
            {
                await store.SetAsync(null);
                _isProtected = false;
                autoBackup.LastFingerprint = null;
                await dialogs.ToastAsync(AppResources.BackupPasswordRemoved);
            }
            OnPropertyChanged(nameof(IsProtected));
        }

        /// <summary>
        /// Restores a backup, plain or encrypted: the saved password is tried first, then the user is asked until the
        /// password is right. Null when the user cancels.
        /// </summary>
        public async Task<RestoreResult?> RestoreAsync(string backupPath)
        {
            var password = await store.GetAsync();
            var typed = false;
            while (true)
            {
                try
                {
                    return await export.RestoreAsync(backupPath, password);
                }
                catch (BackupPasswordException ex)
                {
                    // A wrong saved password is not the user's mistake: they're asked as for any protected backup
                    var message = typed && ex.WrongPassword ? AppResources.RestoreWrongPassword : AppResources.RestorePasswordMessage;
                    password = await dialogs.PasswordAsync(AppResources.RestorePasswordTitle, message);
                    if (password == null)
                        return null;
                    typed = true;
                }
            }
        }
    }
}
