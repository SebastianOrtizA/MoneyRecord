namespace MoneyRecord.Services.Interfaces
{
    /// <summary>
    /// Where the app keeps its files. The app passes the platform's app data folder; tests pass a temp folder.
    /// </summary>
    public interface IAppPaths
    {
        /// <summary>The SQLite database (moneyrecord.db3).</summary>
        string DatabasePath { get; }

        /// <summary>The database while it is encrypted (moneyrecord-encrypted.db3); only one of the two exists.</summary>
        string EncryptedDatabasePath { get; }

        /// <summary>Receipt photos.</summary>
        string ReceiptsFolder { get; }

        /// <summary>Snapshots taken before a restore.</summary>
        string BackupsFolder { get; }

        /// <summary>The app's log files (one per day, the last 7 kept). They never leave the device unless shared.</summary>
        string LogsFolder { get; }
    }
}
