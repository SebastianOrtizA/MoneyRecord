using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.Services.Repositories;

namespace MoneyRecord.Services
{
    public sealed class PayeeService : IPayeeService
    {
        private readonly DatabaseInitializer _dbInitializer;

        public PayeeService(DatabaseInitializer dbInitializer)
        {
            _dbInitializer = dbInitializer;
        }

        public async Task<List<PayeeSummary>> GetAllAsync()
        {
            var db = await _dbInitializer.GetConnectionAsync();
            var rows = await db.QueryAsync<SummaryRow>(
                "SELECT p.Id AS Id, p.Name AS Name, " +
                "(SELECT COUNT(*) FROM \"Transaction\" t WHERE t.PayeeId = p.Id) AS TransactionCount, " +
                "(SELECT MAX(t.Date) FROM \"Transaction\" t WHERE t.PayeeId = p.Id) AS LastUsed, " +
                "(SELECT COUNT(*) FROM \"RecurringTransaction\" r WHERE r.PayeeId = p.Id) AS RecurringCount " +
                "FROM \"Payee\" p");
            return rows
                .Select(r => new PayeeSummary(r.Id, r.Name, r.TransactionCount, r.TransactionCount > 0 ? r.LastUsed : null, r.RecurringCount))
                .OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        public async Task<int?> ResolveAsync(string? name)
        {
            var normalized = PayeeNames.Normalize(name);
            if (normalized.Length == 0)
                return null;

            int? id = null;
            await _dbInitializer.RunInTransactionAsync(db =>
            {
                id = db.Table<Payee>().ToList().FirstOrDefault(p => PayeeNames.Same(p.Name, normalized))?.Id;
                if (id == null)
                {
                    var payee = new Payee { Name = normalized };
                    db.Insert(payee);
                    id = payee.Id;
                }
            });
            return id;
        }

        public async Task<int?> GetLastCategoryAsync(int payeeId, TransactionType type)
        {
            var db = await _dbInitializer.GetConnectionAsync();
            var latest = await db.Table<Transaction>()
                .Where(t => t.PayeeId == payeeId && t.Type == type)
                .OrderByDescending(t => t.Date)
                .FirstOrDefaultAsync();
            return latest?.CategoryId;
        }

        public Task RenameAsync(int payeeId, string name) =>
            _dbInitializer.RunInTransactionAsync(db =>
            {
                var normalized = PayeeNames.Normalize(name);
                if (normalized.Length == 0)
                    throw new InvalidOperationException("A payee needs a name.");
                var payees = db.Table<Payee>().ToList();
                if (payees.Any(p => p.Id != payeeId && PayeeNames.Same(p.Name, normalized)))
                    throw new InvalidOperationException($"Another payee is already called \"{normalized}\".");

                var payee = payees.FirstOrDefault(p => p.Id == payeeId)
                    ?? throw new InvalidOperationException($"Payee {payeeId} does not exist.");
                payee.Name = normalized;
                db.Update(payee);
            });

        public Task MergeAsync(int payeeId, int targetId)
        {
            if (payeeId == targetId)
                throw new ArgumentException("A payee can't be merged into itself.", nameof(targetId));

            return _dbInitializer.RunInTransactionAsync(db =>
            {
                if (db.Find<Payee>(targetId) == null)
                    throw new InvalidOperationException($"Payee {targetId} does not exist.");
                db.Execute("UPDATE \"Transaction\" SET PayeeId = ? WHERE PayeeId = ?", targetId, payeeId);
                db.Execute("UPDATE \"RecurringTransaction\" SET PayeeId = ? WHERE PayeeId = ?", targetId, payeeId);
                db.Delete<Payee>(payeeId);
            });
        }

        public Task DeleteAsync(int payeeId) =>
            _dbInitializer.RunInTransactionAsync(db =>
            {
                db.Execute("UPDATE \"Transaction\" SET PayeeId = NULL WHERE PayeeId = ?", payeeId);
                db.Execute("UPDATE \"RecurringTransaction\" SET PayeeId = NULL WHERE PayeeId = ?", payeeId);
                db.Delete<Payee>(payeeId);
            });

        private sealed class SummaryRow
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public int TransactionCount { get; set; }
            public DateTime LastUsed { get; set; }
            public int RecurringCount { get; set; }
        }
    }
}
