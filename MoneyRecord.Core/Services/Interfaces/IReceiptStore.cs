namespace MoneyRecord.Services.Interfaces
{
    /// <summary>
    /// Writes and reads the files attached to transactions (receipt photos, PDFs). A file is known by its path in
    /// <see cref="IAppPaths.ReceiptsFolder"/> everywhere (database, backups); while Encrypt database is on it is stored
    /// as "path.enc" (AES-256-GCM, key derived from the database key), so the photos are no more readable than the data.
    /// Plain and encrypted files can sit side by side (a switch in progress, a restore); each is read by what exists.
    /// </summary>
    public interface IReceiptStore
    {
        /// <summary>Whether a new file would be stored encrypted (database encryption on and its key available).</summary>
        bool IsEncrypting { get; }

        /// <summary>Saves <paramref name="source"/> as the receipt at <paramref name="path"/>, encrypted when <see cref="IsEncrypting"/>.</summary>
        Task SaveAsync(string path, Stream source);

        /// <summary>The receipt's bytes as a plain stream (decrypted in memory when needed); throws when it is gone or can't be decrypted.</summary>
        Task<Stream> OpenReadAsync(string path);

        /// <summary>
        /// A path to a plain file with the receipt, for apps that need a file: the receipt itself when it is stored plain,
        /// else a decrypted copy in <see cref="IAppPaths.ReceiptViewFolder"/> (see <see cref="ClearViewFolder"/>).
        /// </summary>
        Task<string> GetReadablePathAsync(string path);

        /// <summary>Deletes the decrypted copies made by <see cref="GetReadablePathAsync"/>.</summary>
        void ClearViewFolder();

        /// <summary>Encrypts every plain file in the receipts folder (the database was just encrypted, a restore added plain ones). Nothing to do when none.</summary>
        Task EncryptAllAsync();

        /// <summary>Decrypts every encrypted file (database encryption is being turned off).</summary>
        Task DecryptAllAsync();
    }
}
