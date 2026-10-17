namespace MoneyRecord.Services.Interfaces
{
    public interface IDataExportService
    {
        /// <summary>AppDataDirectory/Backups: local backups and the automatic pre-restore snapshots.</summary>
        string BackupsFolder { get; }

        /// <summary>
        /// Every transaction and transfer as CSV, newest first, or only those dated in [<paramref name="startDate"/>,
        /// <paramref name="endDate"/>]. Amounts are unsigned in the account's currency (invariant culture); a transfer has
        /// its destination in the To columns, the parts of a split expense share a split ID.
        /// </summary>
        Task<string> ExportCsvAsync(DateTime? startDate = null, DateTime? endDate = null);

        /// <summary>
        /// Writes a full backup to <paramref name="destinationPath"/>: a .zip with data.json and the receipt photos.
        /// With a <paramref name="password"/> its entries are AES-256 encrypted. Returns the fingerprint of the data it
        /// holds (see <see cref="GetFingerprintAsync"/>).
        /// </summary>
        Task<string> ExportBackupAsync(string destinationPath, string? password = null);

        /// <summary>A hash of all rows: a backup made now would hold the same data as one with this fingerprint.</summary>
        Task<string> GetFingerprintAsync();

        /// <summary>
        /// Replaces all data with a backup (.zip, or a .json from an older app version). Current data is saved to a
        /// pre-restore snapshot first (never encrypted: it stays in the app); the restore is all-or-nothing, so a failure
        /// leaves the current data untouched. An encrypted backup throws <see cref="BackupPasswordException"/> without its
        /// <paramref name="password"/>; a plain one ignores it.
        /// </summary>
        Task<RestoreResult> RestoreAsync(string backupPath, string? password = null);
    }

    /// <param name="RecordCount">Rows restored.</param>
    /// <param name="SnapshotPath">Backup of the data as it was before the restore (restore it to undo).</param>
    public sealed record RestoreResult(int RecordCount, string SnapshotPath);
}
