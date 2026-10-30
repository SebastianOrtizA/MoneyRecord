using MoneyRecord.Models;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Services
{
    public sealed class GetStartedService(IPreferencesService preferences, ITransactionRepository transactions, IBackupHistory backups,
        IPrivacySettings privacy, IReminderSettings reminders, IDeviceLock deviceLock) : IGetStartedService
    {
        private const string DismissedKey = "FirstRun.ChecklistDismissed";

        // Unset = not decided yet (-1); the first run decides and keeps it
        private const int Unset = -1;

        public async Task<GetStartedChecklist?> GetOpenAsync()
        {
            var hasTransactions = await transactions.AnyAsync();
            var dismissed = preferences.Get(DismissedKey, Unset);
            if (dismissed == Unset)
            {
                dismissed = hasTransactions ? 1 : 0;
                preferences.Set(DismissedKey, dismissed);
            }

            if (dismissed == 1)
                return null;

            var checklist = GetStartedChecklist.Build(new GetStartedState(hasTransactions, backups.LastWritten != null,
                deviceLock.IsSupported && deviceLock.IsAvailable, privacy.LockEnabled, reminders.Enabled));
            if (!checklist.IsComplete)
                return checklist;

            // Done is done: turning something off again later doesn't bring the card back
            Dismiss();
            return null;
        }

        public void Dismiss() => preferences.Set(DismissedKey, 1);
    }
}
