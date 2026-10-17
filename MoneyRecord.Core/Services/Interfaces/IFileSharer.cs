namespace MoneyRecord.Services.Interfaces
{
    /// <summary>Hands a file or text the app wrote to another app (the system share sheet).</summary>
    public interface IFileSharer
    {
        /// <summary>
        /// A new timestamped path in the cache folder, to share. Earlier files with the same prefix are deleted first, so
        /// shared copies (backups include photos) don't pile up.
        /// </summary>
        string NewCacheFile(string prefix, string extension);

        Task ShareAsync(string path, string title);

        /// <summary>Hands plain text to another app (the system share sheet).</summary>
        Task ShareTextAsync(string text, string title);
    }
}
