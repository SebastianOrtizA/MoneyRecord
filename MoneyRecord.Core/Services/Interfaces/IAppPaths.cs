namespace MoneyRecord.Services.Interfaces
{
    /// <summary>
    /// Where the app keeps its files. The app passes the platform's app data folder; tests pass a temp folder.
    /// </summary>
    public interface IAppPaths
    {
        /// <summary>The SQLite database (moneyrecord.db3).</summary>
        string DatabasePath { get; }

        /// <summary>Receipt photos.</summary>
        string ReceiptsFolder { get; }

        /// <summary>Snapshots taken before a restore.</summary>
        string BackupsFolder { get; }
    }
}
