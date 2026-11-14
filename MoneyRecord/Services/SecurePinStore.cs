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
            try
            {
                var value = await SecureStorage.Default.GetAsync(key);
                return string.IsNullOrEmpty(value) ? null : value;
            }
            catch (Exception)
            {
                // The keystore key was invalidated (OS update, screen-lock change): the value is unreadable for good, so
                // it counts as missing and PinService drops the PIN instead of crashing the lock screen
                SecureStorage.Default.Remove(key);
                return null;
            }
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
