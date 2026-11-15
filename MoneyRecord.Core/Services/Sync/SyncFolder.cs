namespace MoneyRecord.Services.Sync
{
    /// <summary>
    /// The shared folder (Drive, Nextcloud, Syncthing, a USB stick) the user chose for syncing while apart: change files are copied
    /// between it and this device's own store, so any device that shares the folder and the sync key gets them. Kept on this device
    /// only (the choice is not synced): Android keeps a document tree from the system picker, Windows a plain path.
    /// </summary>
    public interface ISyncFolder
    {
        bool IsSupported { get; }

        /// <summary>The chosen folder's name; null when none is chosen or it can no longer be reached.</summary>
        string? Name { get; }

        /// <summary>Shows the folder picker and keeps the choice; false when cancelled.</summary>
        Task<bool> PickAsync();

        void Clear();

        /// <summary>
        /// The folder as a store (its change files live in a "MoneyRecordSync" sub-folder); null when none is chosen. May throw when the
        /// saved folder can no longer be reached (permission taken back, provider gone): the sync reports that as FolderUnavailable.
        /// </summary>
        ISyncStore? Open();
    }

    public sealed class NoSyncFolder : ISyncFolder
    {
        public bool IsSupported => false;

        public string? Name => null;

        public Task<bool> PickAsync() => Task.FromResult(false);

        public void Clear() { }

        public ISyncStore? Open() => null;
    }
}
