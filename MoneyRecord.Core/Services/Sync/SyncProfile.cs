namespace MoneyRecord.Services.Sync
{
    /// <summary>A device this one syncs with, and how to reach it (the addresses it showed when they were paired).</summary>
    public sealed record SyncPeer(string DeviceId, string Name, string[] Addresses, int Port, string Fingerprint, DateTimeOffset? LastSync = null);

    /// <summary>
    /// Everything sync keeps in secure storage (never in a backup): the key every device of the group shares, the certificate this
    /// device listens with (if it can), and the devices it connects to.
    /// </summary>
    public sealed class SyncProfile
    {
        /// <summary>The 256-bit sync key, base64.</summary>
        public string Key { get; set; } = "";

        /// <summary>The PFX of the certificate this device listens with, base64; null on a device that never listens.</summary>
        public string? Certificate { get; set; }

        public string DeviceName { get; set; } = "";

        public List<SyncPeer> Peers { get; set; } = [];
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
}
