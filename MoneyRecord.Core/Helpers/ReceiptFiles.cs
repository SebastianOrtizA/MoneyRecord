namespace MoneyRecord.Helpers
{
    /// <summary>
    /// Receipt photos live in AppDataDirectory/receipts (<see cref="Services.Interfaces.IAppPaths.ReceiptsFolder"/>).
    /// </summary>
    public static class ReceiptFiles
    {
        public const string FolderName = "receipts";

        /// <summary>
        /// Deletes a receipt photo. Best effort: a missing file or an I/O error is not an error.
        /// Deletes the file only when it is inside <paramref name="receiptsFolder"/>, so a stray path
        /// (e.g. from an imported backup) can never remove anything else.
        /// </summary>
        public static bool Delete(string? path, string receiptsFolder)
        {
            if (!IsInFolder(path, receiptsFolder) || !File.Exists(path))
                return false;

            try
            {
                File.Delete(path!);
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return false;
            }
        }

        /// <summary>
        /// True when <paramref name="path"/> resolves to a file inside <paramref name="receiptsFolder"/>
        /// ("..", other folders and empty paths are not).
        /// </summary>
        public static bool IsInFolder(string? path, string receiptsFolder)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            var file = Path.GetFullPath(path);
            var folder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(receiptsFolder)) + Path.DirectorySeparatorChar;
            return file.StartsWith(folder, StringComparison.OrdinalIgnoreCase);
        }
    }
}
