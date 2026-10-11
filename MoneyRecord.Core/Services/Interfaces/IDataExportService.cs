namespace MoneyRecord.Services.Interfaces
{
    public interface IDataExportService
    {
        /// <summary>AppDataDirectory/Backups: local backups and the automatic pre-restore snapshots.</summary>
        string BackupsFolder { get; }

        Task<string> ExportCsvAsync();

        /// <summary>
        /// Writes a full backup to <paramref name="destinationPath"/>: a .zip with data.json and the receipt photos.
        /// </summary>
        Task ExportBackupAsync(string destinationPath);

        /// <summary>
        /// Replaces all data with a backup (.zip, or a .json from an older app version). Current data is saved to a
        /// pre-restore snapshot first; the restore is all-or-nothing, so a failure leaves the current data untouched.
        /// </summary>
        Task<RestoreResult> RestoreAsync(string backupPath);
    }

    /// <param name="RecordCount">Rows restored.</param>
    /// <param name="SnapshotPath">Backup of the data as it was before the restore (restore it to undo).</param>
    public sealed record RestoreResult(int RecordCount, string SnapshotPath);
}
