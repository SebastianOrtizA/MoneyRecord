namespace MoneyRecord.Helpers
{
    /// <summary>
    /// Receipt photos live in AppDataDirectory/receipts (<see cref="Services.Interfaces.IAppPaths.ReceiptsFolder"/>).
    /// </summary>
    public static class ReceiptFiles
    {
        public const string FolderName = "receipts";

        /// <summary>Suffix of a receipt file while it is encrypted (see <see cref="Services.Interfaces.IReceiptStore"/>): "receipt_x.jpg.enc".</summary>
        public const string EncryptedSuffix = ".enc";

        /// <summary>The name a receipt goes by in the database and in backups, without the encrypted suffix.</summary>
        public static string LogicalName(string? fileName) =>
            fileName == null ? string.Empty
            : fileName.EndsWith(EncryptedSuffix, StringComparison.OrdinalIgnoreCase) ? fileName[..^EncryptedSuffix.Length] : fileName;

        /// <summary>True when the receipt is in the folder as a plain file or as its encrypted twin.</summary>
        public static bool Exists(string path) => File.Exists(path) || File.Exists(path + EncryptedSuffix);

        /// <summary>
        /// Deletes a receipt photo. Best effort: a missing file or an I/O error is not an error.
        /// Deletes the file only when it is inside <paramref name="receiptsFolder"/>, so a stray path
        /// (e.g. from an imported backup) can never remove anything else.
        /// </summary>
        public static bool Delete(string? path, string receiptsFolder)
        {
            if (!IsInFolder(path, receiptsFolder))
                return false;

            // The plain file and its encrypted twin: whichever exist
            var deleted = false;
            foreach (var file in new[] { path!, path + EncryptedSuffix })
            {
                if (!File.Exists(file))
                    continue;
                try
                {
                    File.Delete(file);
                    deleted = true;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                }
            }
            return deleted;
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
