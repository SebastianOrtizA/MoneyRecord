using System.Security.Cryptography;
using MoneyRecord.Models;
using MoneyRecord.Services.Repositories;
using MoneyRecord.Services.Sync.Lan;

namespace MoneyRecord.Services.Sync
{
    /// <summary>
    /// Where receipt files are kept while they travel (Stage 26.4c): sealed blobs named by the SHA-256 of the plain file, so equal files
    /// are one blob. A blob is only ever the sealed bytes: whoever holds the store (a shared folder, the computer) cannot read it.
    /// </summary>
    public interface IBlobStore
    {
        Task<bool> HasAsync(string hash);

        /// <summary>The sealed blob, or null when there is none.</summary>
        Task<byte[]?> ReadBlobAsync(string hash);

        /// <summary>Writes a blob; one that is already there is replaced with the same content.</summary>
        Task WriteBlobAsync(string hash, byte[] sealedBlob);
    }

    public static class SyncBlobs
    {
        /// <summary>The largest receipt file that travels; a larger one stays on its device (the owner's choice).</summary>
        public const long MaxBytes = 10 * 1024 * 1024;

        /// <summary>What a sealed blob can weigh at most: the file (it may not compress) plus the seal's header.</summary>
        public const int MaxSealedBytes = (int)MaxBytes + 1024;

        /// <summary>A blob name is the lowercase hex SHA-256; the store turns it into a file name, so nothing else is let through.</summary>
        public static bool IsHash(string? name) => name is { Length: 64 } && name.All(c => c is (>= '0' and <= '9') or (>= 'a' and <= 'f'));

        public static string HashOf(byte[] plain) => Convert.ToHexString(SHA256.HashData(plain)).ToLowerInvariant();

        private static string NameOf(string hash) => "blob/" + hash;

        /// <summary>Seals a file; its name is bound into the seal, so a blob stored under another name does not open.</summary>
        public static byte[] Seal(byte[] key, string hash, byte[] plain) => SyncCrypto.Seal(key, NameOf(hash), plain);

        /// <summary>The file, or null when the blob is not what its name says (another key, another name, damaged, a different file).</summary>
        public static byte[]? Open(byte[] key, string hash, byte[] sealedBlob)
        {
            try
            {
                var plain = SyncCrypto.Open(key, NameOf(hash), sealedBlob);
                return plain.Length <= MaxBytes && HashOf(plain) == hash ? plain : null;
            }
            catch (CryptographicException)
            {
                return null;
            }
            catch (InvalidDataException)
            {
                return null;
            }
        }

        /// <summary>
        /// Puts the receipt files on this device into every store that lacks them; returns how many blobs were written. Files larger
        /// than <see cref="MaxBytes"/>, files that are gone and files that changed since they were hashed are left out. A failing
        /// store throws (the caller reports a folder or a link that cannot be reached); the stores before it have what was sent.
        /// </summary>
        public static async Task<int> UploadAsync(DatabaseInitializer database, byte[] key, IReadOnlyList<IBlobStore> targets)
        {
            if (targets.Count == 0)
                return 0;
            var db = await database.GetConnectionAsync();
            var rows = await db.QueryAsync<TransactionAttachment>(
                "SELECT * FROM \"TransactionAttachment\" WHERE ContentHash IS NOT NULL AND Path IS NOT NULL AND Path <> '' AND SizeBytes <= ?", MaxBytes);

            var sent = 0;
            foreach (var group in rows.Where(r => IsHash(r.ContentHash)).GroupBy(r => r.ContentHash!))
            {
                var hash = group.Key;
                var missing = new List<IBlobStore>();
                foreach (var target in targets)
                {
                    if (!await target.HasAsync(hash))
                        missing.Add(target);
                }
                if (missing.Count == 0)
                    continue;

                byte[]? sealedBlob = null;
                foreach (var row in group) // the same file attached twice: any copy will do
                {
                    sealedBlob = await TrySealAsync(key, hash, row.Path);
                    if (sealedBlob != null)
                        break;
                }
                if (sealedBlob == null)
                    continue;

                foreach (var target in missing)
                {
                    await target.WriteBlobAsync(hash, sealedBlob);
                    sent++;
                }
            }
            return sent;
        }

        private static async Task<byte[]?> TrySealAsync(byte[] key, string hash, string path)
        {
            try
            {
                if (!File.Exists(path) || new FileInfo(path).Length > MaxBytes)
                    return null;
                var plain = await File.ReadAllBytesAsync(path);
                return HashOf(plain) == hash ? Seal(key, hash, plain) : null;
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        /// <summary>
        /// The receipt file with this hash from the first store that has it, or null. A store that cannot be reached or holds something
        /// that is not the file is passed over.
        /// </summary>
        public static async Task<byte[]?> FetchAsync(byte[] key, string hash, IEnumerable<IBlobStore> sources)
        {
            if (!IsHash(hash))
                return null;
            foreach (var source in sources)
            {
                try
                {
                    if (await source.ReadBlobAsync(hash) is { } sealedBlob && Open(key, hash, sealedBlob) is { } plain)
                        return plain;
                }
                catch (Exception ex) when (ex is LanSyncException or IOException or UnauthorizedAccessException)
                {
                }
            }
            return null;
        }
    }
}
