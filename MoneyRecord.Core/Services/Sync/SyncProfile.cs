namespace MoneyRecord.Services.Sync
{
    /// <summary>A device this one syncs with, and how to reach it (the addresses it showed when they were paired).</summary>
    public sealed record SyncPeer(string DeviceId, string Name, string[] Addresses, int Port, string Fingerprint, DateTimeOffset? LastSync = null);

    /// <summary>A device that paired with this one (the listening side): its own secret for signing in, which removing it deletes.</summary>
    public sealed record SyncDevice(string DeviceId, string Name, string AuthSecret, DateTimeOffset PairedAt, DateTimeOffset? LastSeen = null);

    /// <summary>
    /// Everything sync keeps in secure storage (never in a backup): the key every device of the group shares, this device's own
    /// secret for signing in, the certificate it listens with (if it can), the devices it connects to, and the devices that paired
    /// with it.
    /// </summary>
    public sealed class SyncProfile
    {
        /// <summary>The 256-bit sync key (it encrypts the change files), base64.</summary>
        public string Key { get; set; } = "";

        /// <summary>This device's own secret for signing in to the device it paired with, base64; empty on the listening side.</summary>
        public string AuthSecret { get; set; } = "";

        /// <summary>The PFX of the certificate this device listens with, base64; null on a device that never listens.</summary>
        public string? Certificate { get; set; }

        public string DeviceName { get; set; } = "";

        /// <summary>The devices this one connects to (a phone: the computer).</summary>
        public List<SyncPeer> Peers { get; set; } = [];

        /// <summary>The devices that paired with this one (the computer: its phones).</summary>
        public List<SyncDevice> Devices { get; set; } = [];
    }

    /// <summary>Where <see cref="SyncProfile"/> is kept: Android secure storage, Windows DPAPI; none elsewhere (sync is not offered).</summary>
    public interface ISyncSecretStore
    {
        bool IsSupported { get; }

        /// <summary>The profile as JSON; null when sync was never turned on.</summary>
        Task<string?> GetAsync();

        /// <summary>Saves the profile; null removes it.</summary>
        Task SetAsync(string? json);
    }

    public sealed class NoSyncSecretStore : ISyncSecretStore
    {
        public bool IsSupported => false;

        public Task<string?> GetAsync() => Task.FromResult<string?>(null);

        public Task SetAsync(string? json) => Task.CompletedTask;
    }

    /// <summary>What differs by platform: whether this device can listen (the desktop app can; the phone connects) and its name.</summary>
    public interface ISyncPlatform
    {
        bool CanListen { get; }

        string DeviceName { get; }
    }

    public sealed class NoSyncPlatform : ISyncPlatform
    {
        public bool CanListen => false;

        public string DeviceName => "Device";
    }

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

        /// <summary>The folder as a store (its change files live in a "MoneyRecordSync" sub-folder); null when none is chosen.</summary>
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

    /// <summary>
    /// Syncing while the app is closed, where the platform has a job for it (Android's WorkManager: the phone connects to the
    /// computer now and then). The computer's app syncs by its own timer while it is open.
    /// </summary>
    public interface ISyncBackground
    {
        /// <summary>Makes sure the periodic job is scheduled (an existing schedule is kept).</summary>
        void Schedule();

        void Cancel();
    }

    public sealed class NoSyncBackground : ISyncBackground
    {
        public void Schedule() { }

        public void Cancel() { }
    }
}
