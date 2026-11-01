using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.Services.Repositories;
using SQLite;

namespace MoneyRecord.Services
{
    public sealed class PayeeService : IPayeeService
    {
        private readonly DatabaseInitializer _dbInitializer;
        private readonly IAccountRepository _accountRepository;

        public PayeeService(DatabaseInitializer dbInitializer, IAccountRepository accountRepository)
        {
            _dbInitializer = dbInitializer;
            _accountRepository = accountRepository;
        }

        public async Task<List<PayeeSummary>> GetAllAsync()
        {
            var db = await _dbInitializer.GetConnectionAsync();
            var rows = await db.QueryAsync<SummaryRow>(
                "SELECT p.Id AS Id, p.Name AS Name, " +
                "(SELECT COUNT(*) FROM \"Transaction\" t WHERE t.PayeeId = p.Id) AS TransactionCount, " +
                "(SELECT MAX(t.Date) FROM \"Transaction\" t WHERE t.PayeeId = p.Id) AS LastUsed, " +
                "(SELECT COUNT(*) FROM \"RecurringTransaction\" r WHERE r.PayeeId = p.Id AND r.DebtId IS NULL) AS RecurringCount, " +
                "(SELECT COUNT(*) FROM \"Debt\" d WHERE d.PayeeId = p.Id) AS DebtCount " +
                "FROM \"Payee\" p");
            return rows
                .Select(r => new PayeeSummary(r.Id, r.Name, r.TransactionCount, r.TransactionCount > 0 ? r.LastUsed : null, r.RecurringCount, r.DebtCount))
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

        public async Task<EntryPrefill?> GetLastEntryAsync(int payeeId, TransactionType type)
        {
            var db = await _dbInitializer.GetConnectionAsync();
            var latest = await db.Table<Transaction>()
                .Where(t => t.PayeeId == payeeId && t.Type == type)
                .OrderByDescending(t => t.Date)
                .FirstOrDefaultAsync();
            return latest == null ? null : EntryPrefill.From(latest);
        }

        public Task RenameAsync(int payeeId, string name) =>
            RenamingDebtsAsync(db =>
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
                RenameDebts(db, payeeId, normalized);
            });

        public Task MergeAsync(int payeeId, int targetId)
        {
            if (payeeId == targetId)
                throw new ArgumentException("A payee can't be merged into itself.", nameof(targetId));

            return RenamingDebtsAsync(db =>
            {
                if (db.Find<Payee>(targetId) == null)
                    throw new InvalidOperationException($"Payee {targetId} does not exist.");
                db.Execute("UPDATE \"Transaction\" SET PayeeId = ? WHERE PayeeId = ?", targetId, payeeId);
                db.Execute("UPDATE \"RecurringTransaction\" SET PayeeId = ? WHERE PayeeId = ?", targetId, payeeId);
                db.Execute("UPDATE \"Debt\" SET PayeeId = ? WHERE PayeeId = ?", targetId, payeeId);
                RenameDebts(db, targetId, db.Find<Payee>(targetId).Name);
                db.Delete<Payee>(payeeId);
            });
        }

        public Task DeleteAsync(int payeeId) =>
            _dbInitializer.RunInTransactionAsync(db =>
            {
                if (db.Table<Debt>().Count(d => d.PayeeId == payeeId) > 0)
                    throw new InvalidOperationException($"Payee {payeeId} has debts; delete them or merge the payee.");
                db.Execute("UPDATE \"Transaction\" SET PayeeId = NULL WHERE PayeeId = ?", payeeId);
                db.Execute("UPDATE \"RecurringTransaction\" SET PayeeId = NULL WHERE PayeeId = ?", payeeId);
                db.Delete<Payee>(payeeId);
            });

        // Runs a change that may rename debts' hidden accounts, which the account repository caches
        private async Task RenamingDebtsAsync(Action<SQLiteConnection> work)
        {
            try
            {
                await _dbInitializer.RunInTransactionAsync(work);
            }
            finally
            {
                _accountRepository.InvalidateCache();
            }
        }

        // A debt is named after its person: its hidden account (the transfers' other side) and its schedule follow the payee
        private static void RenameDebts(SQLiteConnection db, int payeeId, string name)
        {
            foreach (var debt in db.Table<Debt>().Where(d => d.PayeeId == payeeId).ToList())
            {
                db.Execute("UPDATE \"Account\" SET Name = ? WHERE Id = ?", name, debt.AccountId);
                db.Execute("UPDATE \"RecurringTransaction\" SET Description = ? WHERE DebtId = ?",
                    DebtService.ScheduleDescription(debt.Direction, name), debt.Id);
            }
        }

        private sealed class SummaryRow
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public int TransactionCount { get; set; }
            public DateTime LastUsed { get; set; }
            public int RecurringCount { get; set; }
            public int DebtCount { get; set; }
        }
    }
}
