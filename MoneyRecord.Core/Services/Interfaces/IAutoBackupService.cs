namespace MoneyRecord.Services.Interfaces
{
    /// <summary>Automatic backups into the folder the user picked (<see cref="IBackupFolder"/>), keeping the newest few.</summary>
    public interface IAutoBackupService
    {
        /// <summary>
        /// The daily job and the app start: writes a backup when they are on, the folder is reachable, one is due and the
        /// data changed since the last one. Best-effort: a failure is logged and remembered, never thrown.
        /// </summary>
        Task RunIfDueAsync();

        /// <summary>Settings → Back up now: always writes one. A failure is remembered and thrown.</summary>
        Task BackUpNowAsync();
    }
}
