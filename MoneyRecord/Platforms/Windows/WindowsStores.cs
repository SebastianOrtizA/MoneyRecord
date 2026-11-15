using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Platforms.Windows
{
    /// <summary>The database key under DPAPI (<see cref="WindowsSecretStore"/>): this Windows user on this PC only.</summary>
    public sealed class WindowsDatabaseKeyStore : IDatabaseKeyStore
    {
        private const string KeyName = "DatabaseKey";

        public bool IsSupported => true;

        public Task<string?> GetAsync() => WindowsSecretStore.GetAsync(KeyName);

        public Task SetAsync(string key) => WindowsSecretStore.SetAsync(KeyName, key);

        public void Remove() => WindowsSecretStore.Remove(KeyName);
    }

    /// <summary>The backup password under DPAPI (<see cref="WindowsSecretStore"/>), so automatic backups can use it.</summary>
    public sealed class WindowsBackupPasswordStore : IBackupPasswordStore
    {
        private const string KeyName = "BackupPassword";

        public bool IsSupported => true;

        public Task<string?> GetAsync() => WindowsSecretStore.GetAsync(KeyName);

        public Task SetAsync(string? password) => WindowsSecretStore.SetAsync(KeyName, password);
    }

    /// <summary>The sync profile (key, certificate, devices) under DPAPI (<see cref="WindowsSecretStore"/>).</summary>
    public sealed class WindowsSyncSecretStore : MoneyRecord.Services.Sync.ISyncSecretStore
    {
        private const string KeyName = "SyncProfile";

        public bool IsSupported => true;

        public async Task<string?> GetAsync()
        {
            var json = await WindowsSecretStore.GetAsync(KeyName);
            return string.IsNullOrEmpty(json) ? null : json;
        }

        public Task SetAsync(string? json) => WindowsSecretStore.SetAsync(KeyName, json);
    }

    /// <summary>
    /// The PIN hash and lockout state under DPAPI (<see cref="WindowsSecretStore"/>).
    /// </summary>
    public sealed class WindowsPinStore : IPinStore
    {
        private const string HashKey = "AppPinHash";
        private const string LockoutKey = "AppPinLockout";

        public bool IsSupported => true;

        public Task<string?> GetHashAsync() => GetAsync(HashKey);

        public Task SetHashAsync(string? hash) => WindowsSecretStore.SetAsync(HashKey, hash);

        public Task<string?> GetLockoutAsync() => GetAsync(LockoutKey);

        public Task SetLockoutAsync(string? state) => WindowsSecretStore.SetAsync(LockoutKey, state);

        private static async Task<string?> GetAsync(string key)
        {
            var value = await WindowsSecretStore.GetAsync(key);
            return string.IsNullOrEmpty(value) ? null : value;
        }
    }
}
