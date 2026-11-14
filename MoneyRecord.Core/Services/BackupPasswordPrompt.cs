using MoneyRecord.Resources.Strings;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Services
{
    public sealed class BackupPasswordPrompt(IBackupPasswordStore store, IAutoBackupSettings autoBackup, IDialogService dialogs)
        : IBackupPasswordPrompt
    {
        public async Task<bool> SetAsync()
        {
            var password = await dialogs.PasswordAsync(AppResources.BackupPasswordTitle, AppResources.BackupPasswordMessage,
                confirm: true);
            if (string.IsNullOrEmpty(password))
                return false;

            await store.SetAsync(password);
            // The next automatic backup is made with it, even if the data didn't change
            autoBackup.LastFingerprint = null;
            await dialogs.ToastAsync(AppResources.BackupPasswordSaved);
            return true;
        }
    }
}
