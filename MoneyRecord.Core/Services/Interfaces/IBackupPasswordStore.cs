namespace MoneyRecord.Services.Interfaces
{
    /// <summary>
    /// The backup password (Settings → Backup &amp; Restore), kept in secure storage so automatic backups can use it.
    /// None = backups are plain zips.
    /// </summary>
    public interface IBackupPasswordStore
    {
        Task<string?> GetAsync();

        /// <summary>Saves the password; null or empty removes it.</summary>
        Task SetAsync(string? password);
    }
}
