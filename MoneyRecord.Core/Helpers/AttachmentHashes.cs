using System.Security.Cryptography;
using MoneyRecord.Models;
using MoneyRecord.Services.Repositories;

namespace MoneyRecord.Helpers
{
    /// <summary>
    /// Names the receipt files for sync (Stage 26.4): the SHA-256 and size of every attached file that is on this device and not
    /// hashed yet. The hash is what the other devices call the file, so the same file is one blob however often it is attached.
    /// </summary>
    public static class AttachmentHashes
    {
        /// <summary>Hashes the files; returns how many rows were updated. A file that is gone or unreadable is skipped (tried again next time).</summary>
        public static async Task<int> FillAsync(DatabaseInitializer database)
        {
            var db = await database.GetConnectionAsync();
            var pending = await db.QueryAsync<TransactionAttachment>(
                "SELECT * FROM \"TransactionAttachment\" WHERE ContentHash IS NULL AND Path IS NOT NULL AND Path <> ''");
            var hashed = new List<(int Id, string Hash, long Size)>();
            foreach (var attachment in pending)
            {
                try
                {
                    if (!File.Exists(attachment.Path))
                        continue;
                    await using var stream = File.OpenRead(attachment.Path);
                    var hash = Convert.ToHexString(await SHA256.HashDataAsync(stream)).ToLowerInvariant();
                    hashed.Add((attachment.Id, hash, stream.Length));
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
            if (hashed.Count == 0)
                return 0;

            await database.RunInTransactionAsync(conn =>
            {
                foreach (var (id, hash, size) in hashed)
                    conn.Execute("UPDATE \"TransactionAttachment\" SET ContentHash = ?, SizeBytes = ? WHERE Id = ? AND ContentHash IS NULL", hash, size, id);
            });
            return hashed.Count;
        }
    }
}
