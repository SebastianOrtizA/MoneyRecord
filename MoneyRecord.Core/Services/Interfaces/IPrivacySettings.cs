namespace MoneyRecord.Services.Interfaces
{
    /// <summary>The app lock (Settings) and hidden balances (Home), saved in the preferences.</summary>
    public interface IPrivacySettings
    {
        /// <summary>Opening the app, or coming back after <see cref="LockAfterMinutes"/>, asks for the phone's lock.</summary>
        bool LockEnabled { get; set; }

        /// <summary>How long the app may stay in the background before it locks (one of <see cref="PrivacySettings.LockDelays"/>).</summary>
        int LockAfterMinutes { get; set; }

        /// <summary>Home shows "••••••" instead of the balance and the period totals.</summary>
        bool HideBalances { get; set; }

        /// <summary>What opens the lock screen: the phone's lock, the app PIN, or either.</summary>
        Models.LockMethod LockMethod { get; set; }

        /// <summary>An app PIN is set (its hash is in <see cref="IPinStore"/>); a flag so the lock can decide without secure storage.</summary>
        bool HasPin { get; set; }

        /// <summary>Screenshots, screen recordings and the recent apps card show nothing of the app (the lock screen is always hidden).</summary>
        bool BlockScreenshots { get; set; }

        /// <summary>The one-time "protect your backups with a password" offer was already shown.</summary>
        bool BackupNudgeShown { get; set; }
    }
}
