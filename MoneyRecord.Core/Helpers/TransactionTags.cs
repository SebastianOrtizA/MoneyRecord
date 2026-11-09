using MoneyRecord.Models;
using SQLite;

namespace MoneyRecord.Helpers
{
    /// <summary>Reads and writes the comma-separated <see cref="Transaction.Tags"/> and keeps the <see cref="TransactionTag"/> table in step with it.</summary>
    public static class TransactionTags
    {
        /// <summary>A tag as stored: trimmed, inner whitespace collapsed, no comma (it separates tags). Empty = not a tag.</summary>
        public static string Normalize(string? tag) =>
            string.Join(' ', (tag ?? string.Empty).Replace(',', ' ').Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        /// <summary>The tags of a comma-separated list, once each (ignoring case), in order.</summary>
        public static List<string> Split(string? tags) =>
            (tags ?? string.Empty).Split(',')
                .Select(Normalize)
                .Where(t => t.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

        /// <summary>The list as stored; null when empty.</summary>
        public static string? Join(IEnumerable<string> tags)
        {
            var list = tags.ToList();
            return list.Count == 0 ? null : string.Join(", ", list);
        }

        /// <summary><paramref name="tags"/> with <paramref name="from"/> (ignoring case) replaced by <paramref name="to"/> (null = removed), once each.</summary>
        public static string? Replace(string? tags, string from, string? to)
        {
            var result = Split(tags)
                .Select(t => string.Equals(t, from, StringComparison.OrdinalIgnoreCase) ? to : t)
                .OfType<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase);
            return Join(result);
        }

        /// <summary>Makes the table rows of these transactions match their <see cref="Transaction.Tags"/>. Call after the transactions are saved.</summary>
        public static void Sync(SQLiteConnection db, IEnumerable<Transaction> transactions)
        {
            foreach (var transaction in transactions)
            {
                db.Execute("DELETE FROM \"TransactionTag\" WHERE TransactionId = ?", transaction.Id);
                foreach (var tag in Split(transaction.Tags))
                    db.Insert(new TransactionTag { TransactionId = transaction.Id, Tag = tag });
            }
        }

        /// <summary>Rebuilds the whole table from the transactions' <see cref="Transaction.Tags"/> (a migration, a restore).</summary>
        public static void RebuildAll(SQLiteConnection db)
        {
            db.DeleteAll<TransactionTag>();
            Sync(db, db.Query<Transaction>("SELECT * FROM \"Transaction\" WHERE Tags IS NOT NULL AND Tags <> ''"));
        }
    }
}
