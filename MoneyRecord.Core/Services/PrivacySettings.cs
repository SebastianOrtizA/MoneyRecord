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

        public bool HideBalances
        {
            get => preferences.Get(HideBalancesKey, false);
            set => preferences.Set(HideBalancesKey, value);
        }
    }
}
