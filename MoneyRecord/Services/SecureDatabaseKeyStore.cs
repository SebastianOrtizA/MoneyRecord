using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Services
{
    /// <summary>
    /// The database key in MAUI SecureStorage. On Android its values are encrypted with a key in the Android Keystore,
    /// which never leaves the phone; the storage file is kept out of Android backup (Resources/xml).
    /// </summary>
    public sealed class SecureDatabaseKeyStore : IDatabaseKeyStore
    {
        private const string KeyName = "DatabaseKey";

        public bool IsSupported => true;

        public Task<string?> GetAsync() => SecureStorage.Default.GetAsync(KeyName);

        public Task SetAsync(string key) => SecureStorage.Default.SetAsync(KeyName, key);

        public void Remove() => SecureStorage.Default.Remove(KeyName);
    }
}
