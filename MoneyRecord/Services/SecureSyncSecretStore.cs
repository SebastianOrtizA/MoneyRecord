using MoneyRecord.Services.Sync;

namespace MoneyRecord.Services
{
    /// <summary>
    /// The sync profile (key, certificate, devices) in MAUI SecureStorage: on Android encrypted with a Keystore key, and kept out of
    /// Android backup with the rest of the secure preferences.
    /// </summary>
    public sealed class SecureSyncSecretStore : ISyncSecretStore
    {
        private const string KeyName = "SyncProfile";

        public bool IsSupported => true;

        public async Task<string?> GetAsync()
        {
            var json = await SecureStorage.Default.GetAsync(KeyName);
            return string.IsNullOrEmpty(json) ? null : json;
        }

        public Task SetAsync(string? json)
        {
            if (string.IsNullOrEmpty(json))
            {
                SecureStorage.Default.Remove(KeyName);
                return Task.CompletedTask;
            }
            return SecureStorage.Default.SetAsync(KeyName, json);
        }
    }
}
