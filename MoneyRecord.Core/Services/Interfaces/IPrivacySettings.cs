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
    }
}
