namespace MoneyRecord.Services.Interfaces
{
    /// <summary>
    /// The folder the user picked for automatic backups (Android: the system folder picker, so it can be on an SD card
    /// or in a cloud app such as Google Drive). Other platforms don't support it yet: Settings hides the card.
    /// Everything but <see cref="PickAsync"/> may block on the provider (a cloud folder): call it off the UI thread.
    /// </summary>
    public interface IBackupFolder
    {
        bool IsSupported { get; }

        /// <summary>The chosen folder's name; null when none was chosen or the app can no longer reach it.</summary>
        string? Name { get; }

        /// <summary>Shows the folder picker and keeps the choice; false when cancelled.</summary>
        Task<bool> PickAsync();

        /// <summary>The names of the files in the folder.</summary>
        IReadOnlyList<string> ListFiles();

        /// <summary>Copies <paramref name="sourcePath"/> into the folder as a new file named <paramref name="fileName"/>.</summary>
        void Write(string fileName, string sourcePath);

        void Delete(string fileName);
    }
}
