using System.Security.Cryptography;
using System.Text;
using MoneyRecord.Helpers;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Services
{
    /// <summary>
    /// <see cref="IReceiptStore"/>. Encrypted file: "MRE1", 12-byte nonce, 16-byte tag, ciphertext. The key is HKDF-SHA256 of the
    /// database key, so there is one secret to keep and turning encryption off decrypts both.
    /// </summary>
    public sealed class ReceiptStore : IReceiptStore
    {
        private static readonly byte[] Magic = "MRE1"u8.ToArray();
        private static readonly byte[] KeyInfo = "MoneyRecord receipts v1"u8.ToArray();
        private const int NonceSize = 12;
        private const int TagSize = 16;
        private const string TempSuffix = ".tmp";

        private readonly IAppPaths _paths;
        private readonly IDatabaseKeyStore _keyStore;
        private byte[]? _key;

        public ReceiptStore(IAppPaths paths, IDatabaseKeyStore? keyStore = null)
        {
            _paths = paths;
            _keyStore = keyStore ?? NoDatabaseKeyStore.Instance;
        }

        // The database is encrypted exactly when its encrypted file exists (DatabaseInitializer.IsEncrypted)
        public bool IsEncrypting => File.Exists(_paths.EncryptedDatabasePath) && _keyStore.IsSupported;

        public async Task SaveAsync(string path, Stream source)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            using var buffer = new MemoryStream();
            await source.CopyToAsync(buffer);

            // A file of the other kind with the same name (a re-save) must not outlive the new one
            if (IsEncrypting && await GetKeyAsync() is { } key)
            {
                await WriteAtomicAsync(path + ReceiptFiles.EncryptedSuffix, Encrypt(buffer.ToArray(), key));
                TryDelete(path);
            }
            else
            {
                await WriteAtomicAsync(path, buffer.ToArray());
                TryDelete(path + ReceiptFiles.EncryptedSuffix);
            }
        }

        public async Task<Stream> OpenReadAsync(string path)
        {
            // Plain wins: a restore or a switch in progress leaves the newer copy plain
            if (File.Exists(path))
                return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);

            var encrypted = path + ReceiptFiles.EncryptedSuffix;
            if (!File.Exists(encrypted))
                throw new FileNotFoundException("The receipt is gone.", path);

            var key = await GetKeyAsync() ?? throw new CryptographicException("The receipt key is not available.");
            return new MemoryStream(Decrypt(await File.ReadAllBytesAsync(encrypted), key));
        }

        public async Task<string> GetReadablePathAsync(string path)
        {
            if (File.Exists(path))
                return path;

            Directory.CreateDirectory(_paths.ReceiptViewFolder);
            var copy = Path.Combine(_paths.ReceiptViewFolder, Path.GetFileName(path));
            await using var plain = await OpenReadAsync(path);
            await using (var output = File.Create(copy))
                await plain.CopyToAsync(output);
            return copy;
        }

        public void ClearViewFolder()
        {
            try
            {
                if (Directory.Exists(_paths.ReceiptViewFolder))
                    Directory.Delete(_paths.ReceiptViewFolder, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A viewer may still hold a file; the next start tries again
            }
        }

        public async Task EncryptAllAsync()
        {
            _key = null; // a new database key (after turning encryption off and on) must not meet the old one
            if (!Directory.Exists(_paths.ReceiptsFolder))
                return;
            var plainFiles = Directory.GetFiles(_paths.ReceiptsFolder)
                .Where(f => !f.EndsWith(ReceiptFiles.EncryptedSuffix, StringComparison.OrdinalIgnoreCase)
                            && !f.EndsWith(TempSuffix, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (plainFiles.Count == 0)
                return;

            var key = await GetKeyAsync() ?? throw new CryptographicException("The receipt key is not available.");
            foreach (var file in plainFiles)
            {
                await WriteAtomicAsync(file + ReceiptFiles.EncryptedSuffix, Encrypt(await File.ReadAllBytesAsync(file), key));
                TryDelete(file);
            }
        }

        public async Task DecryptAllAsync()
        {
            _key = null;
            if (!Directory.Exists(_paths.ReceiptsFolder))
                return;
            var encryptedFiles = Directory.GetFiles(_paths.ReceiptsFolder, "*" + ReceiptFiles.EncryptedSuffix);
            if (encryptedFiles.Length == 0)
                return;

            var key = await GetKeyAsync() ?? throw new CryptographicException("The receipt key is not available.");
            foreach (var file in encryptedFiles)
            {
                byte[] plain;
                try
                {
                    plain = Decrypt(await File.ReadAllBytesAsync(file), key);
                }
                catch (CryptographicException)
                {
                    continue; // damaged: unreadable either way, and one bad file must not keep the switch from finishing
                }
                await WriteAtomicAsync(file[..^ReceiptFiles.EncryptedSuffix.Length], plain);
                TryDelete(file);
            }
        }

        private async Task<byte[]?> GetKeyAsync()
        {
            if (_key != null)
                return _key;
            var databaseKey = await _keyStore.GetAsync();
            if (string.IsNullOrEmpty(databaseKey))
                return null;
            return _key = HKDF.DeriveKey(HashAlgorithmName.SHA256, Encoding.UTF8.GetBytes(databaseKey), 32, info: KeyInfo);
        }

        private static byte[] Encrypt(byte[] plain, byte[] key)
        {
            var result = new byte[Magic.Length + NonceSize + TagSize + plain.Length];
            Magic.CopyTo(result, 0);
            var nonce = result.AsSpan(Magic.Length, NonceSize);
            RandomNumberGenerator.Fill(nonce);
            using var aes = new AesGcm(key, TagSize);
            aes.Encrypt(nonce, plain, result.AsSpan(Magic.Length + NonceSize + TagSize), result.AsSpan(Magic.Length + NonceSize, TagSize));
            return result;
        }

        private static byte[] Decrypt(byte[] data, byte[] key)
        {
            var headerSize = Magic.Length + NonceSize + TagSize;
            if (data.Length < headerSize || !data.AsSpan(0, Magic.Length).SequenceEqual(Magic))
                throw new CryptographicException("Not an encrypted receipt.");

            var plain = new byte[data.Length - headerSize];
            using var aes = new AesGcm(key, TagSize);
            aes.Decrypt(data.AsSpan(Magic.Length, NonceSize), data.AsSpan(headerSize), data.AsSpan(Magic.Length + NonceSize, TagSize), plain);
            return plain;
        }

        /// <summary>Written next to the target and moved in, so a crash never leaves half a file under the real name.</summary>
        private static async Task WriteAtomicAsync(string path, byte[] bytes)
        {
            var temp = path + TempSuffix;
            await File.WriteAllBytesAsync(temp, bytes);
            File.Move(temp, path, overwrite: true);
        }

        private static void TryDelete(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }
    }
}
