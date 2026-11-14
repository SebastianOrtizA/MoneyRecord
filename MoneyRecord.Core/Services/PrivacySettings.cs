using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Services
{
    public sealed class PrivacySettings(IPreferencesService preferences) : IPrivacySettings
    {
        /// <summary>
        /// The lock delays Settings offers, in minutes. No "immediately": taking a receipt photo, picking a file or
        /// sharing leaves the app for a moment, and must not lock it.
        /// </summary>
        public static readonly IReadOnlyList<int> LockDelays = [1, 5, 15, 60];

        private const string LockEnabledKey = "Privacy.LockEnabled";
        private const string LockAfterKey = "Privacy.LockAfterMinutes";
        private const string HideBalancesKey = "Privacy.HideBalances";
        private const string BackupNudgeShownKey = "Privacy.BackupNudgeShown";
        private const string LockMethodKey = "Privacy.LockMethod";
        private const string HasPinKey = "Privacy.HasPin";
        private const string BlockScreenshotsKey = "Privacy.BlockScreenshots";

        public bool LockEnabled
        {
            get => preferences.Get(LockEnabledKey, false);
            set => preferences.Set(LockEnabledKey, value);
        }

        public int LockAfterMinutes
        {
            get => preferences.Get(LockAfterKey, LockDelays[0]);
            set => preferences.Set(LockAfterKey, value);
        }

        public Models.LockMethod LockMethod
        {
            get => Enum.TryParse<Models.LockMethod>(preferences.Get(LockMethodKey, nameof(Models.LockMethod.DeviceLock)), out var method) ? method : Models.LockMethod.DeviceLock;
            set => preferences.Set(LockMethodKey, value.ToString());
        }

        public bool HasPin
        {
            get => preferences.Get(HasPinKey, false);
            set => preferences.Set(HasPinKey, value);
        }

        public bool BlockScreenshots
        {
            get => preferences.Get(BlockScreenshotsKey, false);
            set => preferences.Set(BlockScreenshotsKey, value);
        }

        public bool BackupNudgeShown
        {
            get => preferences.Get(BackupNudgeShownKey, false);
            set => preferences.Set(BackupNudgeShownKey, value);
        }

        public bool HideBalances
        {
            get => preferences.Get(HideBalancesKey, false);
            set => preferences.Set(HideBalancesKey, value);
        }
    }
}
