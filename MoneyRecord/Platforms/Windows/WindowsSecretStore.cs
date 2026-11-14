using System.Security.Cryptography;
using System.Text;

namespace MoneyRecord.Platforms.Windows
{
    /// <summary>
    /// Secrets (database key, backup password, PIN hash) as files in the app data folder, each protected with DPAPI for
    /// the current Windows user: only this user on this PC can read them. MAUI's SecureStorage needs a packaged app, and
    /// the Windows build is a plain zip. Like on Android they stay out of backups (data.json never includes them).
    /// </summary>
    public static class WindowsSecretStore
    {
        private static readonly byte[] Entropy = "MoneyRecord secrets v1"u8.ToArray();

        private static string FolderPath => Path.Combine(FileSystem.AppDataDirectory, "secrets");

        private static string FilePath(string name) => Path.Combine(FolderPath, name + ".bin");

        /// <summary>The secret, null when never set. Null too when it can't be decrypted (other user, other PC).</summary>
        public static Task<string?> GetAsync(string name)
        {
            var path = FilePath(name);
            if (!File.Exists(path))
                return Task.FromResult<string?>(null);
            try
            {
                var plain = ProtectedData.Unprotect(File.ReadAllBytes(path), Entropy, DataProtectionScope.CurrentUser);
                return Task.FromResult<string?>(Encoding.UTF8.GetString(plain));
            }
            catch (CryptographicException)
            {
                // A profile copied to another user or PC: gone for good, like a missing value (SetAsync overwrites the file)
                return Task.FromResult<string?>(null);
            }
        }

        /// <summary>Saves the secret (through a temp file, so a crash can't leave half of it); null or empty removes it.</summary>
        public static Task SetAsync(string name, string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                Remove(name);
                return Task.CompletedTask;
            }
            Directory.CreateDirectory(FolderPath);
            var path = FilePath(name);
            var temp = path + ".tmp";
            File.WriteAllBytes(temp,
                ProtectedData.Protect(Encoding.UTF8.GetBytes(value), Entropy, DataProtectionScope.CurrentUser));
            File.Move(temp, path, overwrite: true);
            return Task.CompletedTask;
        }

        public static void Remove(string name)
        {
            var path = FilePath(name);
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
