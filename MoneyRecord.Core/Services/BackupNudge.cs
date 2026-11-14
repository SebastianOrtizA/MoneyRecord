using Microsoft.Extensions.Logging;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.ViewModels;

namespace MoneyRecord.Services
{
    public sealed class BackupNudge(
        IBackupPasswordStore store,
        IPrivacySettings privacy,
        IDialogService dialogs,
        BackupPasswordViewModel password,
        ILogger<BackupNudge> logger) : IBackupNudge
    {
        public async Task OfferAsync()
        {
            if (!store.IsSupported || privacy.BackupNudgeShown)
                return;

            try
            {
                if (await store.GetAsync() != null)
                    return;

                // Once, whatever the answer: the switch in Backup & Restore stays for later
                privacy.BackupNudgeShown = true;
                if (await dialogs.ConfirmAsync(AppResources.BackupNudgeTitle, AppResources.BackupNudgeMessage,
                        AppResources.BackupNudgeSet, AppResources.BackupNudgeLater))
                    await password.SetPasswordCommand.ExecuteAsync(null);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "The backup password offer failed");
            }
        }
    }
}
