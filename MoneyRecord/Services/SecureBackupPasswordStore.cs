using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Services
{
    /// <summary>
    /// The backup password in MAUI SecureStorage (on Android encrypted with an Android Keystore key, and kept out of
    /// Android backup like the database key).
    /// </summary>
    public sealed class SecureBackupPasswordStore : IBackupPasswordStore
    {
        private const string KeyName = "BackupPassword";

        public async Task<string?> GetAsync()
        {
            var password = await SecureStorage.Default.GetAsync(KeyName);
            return string.IsNullOrEmpty(password) ? null : password;
        }

        public Task SetAsync(string? password)
        {
            if (string.IsNullOrEmpty(password))
            {
                SecureStorage.Default.Remove(KeyName);
                return Task.CompletedTask;
            }
            return SecureStorage.Default.SetAsync(KeyName, password);
        }
    }
}
