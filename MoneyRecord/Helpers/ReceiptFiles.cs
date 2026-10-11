namespace MoneyRecord.Helpers
{
    /// <summary>
    /// Receipt photos live in AppDataDirectory/receipts.
    /// </summary>
    public static class ReceiptFiles
    {
        public static string Folder => Path.Combine(FileSystem.AppDataDirectory, "receipts");

        /// <summary>
        /// Deletes a transaction's receipt photo. Best effort: a missing file or an I/O error is not an error.
        /// </summary>
        public static bool Delete(string? path) => Delete(path, Folder);

        /// <summary>
        /// Deletes the file only when it is inside <paramref name="receiptsFolder"/>, so a stray path
        /// (e.g. from an imported backup) can never remove anything else.
        /// </summary>
        public static bool Delete(string? path, string receiptsFolder)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            var file = Path.GetFullPath(path);
            var folder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(receiptsFolder)) + Path.DirectorySeparatorChar;
            if (!file.StartsWith(folder, StringComparison.OrdinalIgnoreCase) || !File.Exists(file))
                return false;

            try
            {
                File.Delete(file);
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return false;
            }
        }
    }
}
