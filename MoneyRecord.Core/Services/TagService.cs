using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.Services.Repositories;

namespace MoneyRecord.Services
{
    public sealed class TagService(DatabaseInitializer database) : ITagService
    {
        public async Task<List<TagSummary>> GetAllAsync()
        {
            var db = await database.GetConnectionAsync();
            var rows = await db.QueryAsync<UseRow>(
                "SELECT tt.Tag AS Tag, COUNT(*) AS Uses, MAX(t.Date) AS LastTicks FROM \"TransactionTag\" tt " +
                "JOIN \"Transaction\" t ON t.Id = tt.TransactionId GROUP BY tt.Tag");
            // Grouped here, not in SQL: its NOCASE only knows ASCII letters
            return rows
                .GroupBy(r => r.Tag, StringComparer.OrdinalIgnoreCase)
                .Select(g => new TagSummary(g.OrderBy(r => r.Tag, StringComparer.Ordinal).First().Tag, g.Sum(r => r.Uses),
                    new DateTime(g.Max(r => r.LastTicks))))
                .OrderBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        public Task<int> RenameAsync(string tag, string newName)
        {
            var name = TransactionTags.Normalize(newName);
            if (name.Length == 0)
                throw new InvalidOperationException("A tag needs a name.");

            return ChangeAsync(tag, name);
        }

        public Task<int> DeleteAsync(string tag) => ChangeAsync(tag, null);

        // Rewrites the comma-separated tags of every transaction that has the tag, then their table rows, in one transaction
        private async Task<int> ChangeAsync(string tag, string? replacement)
        {
            var changed = 0;
            await database.RunInTransactionAsync(db =>
            {
                var variants = db.Query<TagRow>("SELECT DISTINCT Tag FROM \"TransactionTag\"")
                    .Select(r => r.Tag)
                    .Where(t => string.Equals(t, tag, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (variants.Count == 0)
                    return;

                var ids = db.Query<IdRow>(
                        $"SELECT DISTINCT TransactionId AS Id FROM \"TransactionTag\" WHERE Tag IN ({string.Join(",", variants.Select(_ => "?"))})",
                        [.. variants])
                    .Select(r => r.Id)
                    .ToList();
                var transactions = ids.Select(id => db.Find<Transaction>(id)).OfType<Transaction>().ToList();
                foreach (var transaction in transactions)
                {
                    transaction.Tags = TransactionTags.Replace(transaction.Tags, tag, replacement);
                    db.Update(transaction);
                }

                TransactionTags.Sync(db, transactions);
                changed = transactions.Count;
            });
            return changed;
        }

        private sealed class UseRow
        {
            public string Tag { get; set; } = string.Empty;
            public int Uses { get; set; }
            public long LastTicks { get; set; }
        }

        private sealed class TagRow
        {
            public string Tag { get; set; } = string.Empty;
        }

        private sealed class IdRow
        {
            public int Id { get; set; }
        }
    }
}
