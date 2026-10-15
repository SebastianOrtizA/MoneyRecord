namespace MoneyRecord.Services.Interfaces
{
    /// <summary>
    /// The optional database encryption (Settings → Encrypt database). The encrypted database is a different file,
    /// which Android backup doesn't include: its key never leaves the phone.
    /// </summary>
    public interface IDatabaseEncryption
    {
        /// <summary>The platform can keep the key (Android); elsewhere Settings hides the switch.</summary>
        bool IsSupported { get; }

        bool IsEncrypted { get; }

        /// <summary>Encrypts the database with a new key. The plain file is removed only once the encrypted copy is checked.</summary>
        Task EncryptAsync();

        /// <summary>Decrypts the database and forgets the key.</summary>
        Task DecryptAsync();

        /// <summary>
        /// True once after the start found an encrypted database it couldn't open (its key is gone): the file was set
        /// aside and a new database started.
        /// </summary>
        bool TakeUnreadableDatabaseNotice();
    }
}
