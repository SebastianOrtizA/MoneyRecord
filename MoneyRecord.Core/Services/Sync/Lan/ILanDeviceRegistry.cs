namespace MoneyRecord.Services.Sync.Lan
{
    /// <summary>
    /// The devices the listening side has paired with. Each gets its own secret when it pairs, and signs in with that, not with the
    /// key all devices share for the change files: taking a device out of the registry locks it out at once.
    /// </summary>
    public interface ILanDeviceRegistry
    {
        /// <summary>The device's own secret; null when it is not (or no longer) paired.</summary>
        Task<byte[]?> SecretOfAsync(string deviceId);

        /// <summary>Pairs a device that showed a valid code: remembers it and returns its new secret.</summary>
        Task<byte[]> RegisterAsync(string deviceId, string name);

        /// <summary>The device signed in (for "last seen").</summary>
        Task SeenAsync(string deviceId);
    }
}
