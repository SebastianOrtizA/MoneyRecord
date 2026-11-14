using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Services
{
    /// <summary>
    /// The PIN hash and the lockout state in MAUI SecureStorage (on Android encrypted with an Android Keystore key, and
    /// kept out of Android backup like the database key). The lockout lives here, not in the preferences, so clearing or
    /// editing them can't reset the wrong-try counter.
    /// </summary>
    public sealed class SecurePinStore : IPinStore
    {
        private const string HashKey = "AppPinHash";
        private const string LockoutKey = "AppPinLockout";

        public bool IsSupported => true;

        public async Task<string?> GetHashAsync() => await GetAsync(HashKey);

        public Task SetHashAsync(string? hash) => SetAsync(HashKey, hash);

        public async Task<string?> GetLockoutAsync() => await GetAsync(LockoutKey);

        public Task SetLockoutAsync(string? state) => SetAsync(LockoutKey, state);

        private static async Task<string?> GetAsync(string key)
        {
            var value = await SecureStorage.Default.GetAsync(key);
            return string.IsNullOrEmpty(value) ? null : value;
        }

        private static Task SetAsync(string key, string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                SecureStorage.Default.Remove(key);
                return Task.CompletedTask;
            }
            return SecureStorage.Default.SetAsync(key, value);
        }
    }
}
