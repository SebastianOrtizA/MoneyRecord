namespace MoneyRecord.Services.Interfaces
{
    /// <summary>When a backup was last written, whoever asked for it (Settings, share, or the automatic schedule).</summary>
    public interface IBackupHistory
    {
        /// <summary>The last backup written (local time); null = never.</summary>
        DateTime? LastWritten { get; }

        void MarkWritten();
    }
}
