using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.Services.Repositories;
using SQLite;

namespace MoneyRecord.Services
{
    public sealed class QuickEntryService(DatabaseInitializer database, IAccountRepository accounts, ICategoryRepository categories,
        TimeProvider clock) : IQuickEntryService
    {
        /// <summary>Repeats of the same entry that make it worth a quick entry, within <see cref="SuggestionDays"/>.</summary>
        public const int SuggestionRepeats = 3;

        public const int SuggestionDays = 60;

        public async Task<List<QuickEntryItem>> GetAllAsync()
        {
            var db = await database.GetConnectionAsync();
            var entries = (await db.Table<QuickEntry>().ToListAsync())
                .OrderBy(e => e.SortOrder).ThenBy(e => e.Id).ToList();
            if (entries.Count == 0)
                return [];

            var categoryById = (await categories.GetAllAsync()).ToDictionary(c => c.Id);
            var accountById = (await accounts.GetAllAsync()).ToDictionary(a => a.Id);
            return entries.Select(e =>
            {
                categoryById.TryGetValue(e.CategoryId, out var category);
                accountById.TryGetValue(e.AccountId, out var account);
                return new QuickEntryItem(e, category?.Name ?? string.Empty, category?.IconCode ?? IconHelper.DefaultCategoryIconCode,
                    account?.Name ?? string.Empty, account?.CurrencyCode ?? string.Empty);
            }).ToList();
        }

        public async Task<QuickEntry?> GetAsync(int id)
        {
            var db = await database.GetConnectionAsync();
            return await db.FindAsync<QuickEntry>(id);
        }

        public async Task<int> SaveAsync(QuickEntry entry)
        {
            entry.Name = entry.Name.Trim();
            entry.Description = entry.Description.Trim();
            if (entry.Name.Length == 0)
                throw new InvalidOperationException("A quick entry needs a name.");
            if (entry.Type == TransactionType.Transfer)
                throw new InvalidOperationException("A quick entry is an expense or an income.");
            if (entry.AmountCents < 0)
                throw new InvalidOperationException("The amount can't be negative.");

            await database.RunInTransactionAsync(db =>
            {
                var category = db.Find<Category>(entry.CategoryId)
                    ?? throw new InvalidOperationException($"Category {entry.CategoryId} does not exist.");
                var wanted = entry.Type == TransactionType.Income ? CategoryType.Income : CategoryType.Expense;
                if (category.Type != wanted || category.IsTravel)
                    throw new InvalidOperationException($"Category {entry.CategoryId} doesn't fit a {entry.Type}.");
                if (db.Find<Account>(entry.AccountId) is not { IsDebt: false })
                    throw new InvalidOperationException($"Account {entry.AccountId} does not exist.");
                if (entry.PayeeId is { } payeeId && db.Find<Payee>(payeeId) == null)
                    entry.PayeeId = null;

                if (entry.Id == 0)
                {
                    entry.SortOrder = db.Table<QuickEntry>().ToList().Select(e => e.SortOrder + 1).DefaultIfEmpty(0).Max();
                    db.Insert(entry);
                }
                else
                {
                    db.Update(entry);
                }
            });
            return entry.Id;
        }

        public async Task<QuickEntry?> DraftFromTransactionAsync(int transactionId)
        {
            var db = await database.GetConnectionAsync();
            var transaction = await db.FindAsync<Transaction>(transactionId);
            if (transaction is not { AccountId: { } accountId } || transaction.Type == TransactionType.Transfer
                || transaction.TripId != null)
                return null;

            var category = await db.FindAsync<Category>(transaction.CategoryId);
            if (category == null || category.IsTravel)
                return null;

            string name = category.Name;
            if (transaction.PayeeId is { } payeeId && await db.FindAsync<Payee>(payeeId) is { } payee)
                name = payee.Name;
            else if (!string.IsNullOrWhiteSpace(transaction.Description))
                name = transaction.Description.Trim();

            return new QuickEntry
            {
                Name = name,
                Type = transaction.Type,
                AmountCents = transaction.AmountCents,
                CategoryId = transaction.CategoryId,
                AccountId = accountId,
                PayeeId = transaction.PayeeId,
                Description = transaction.Description == category.Name ? string.Empty : transaction.Description
            };
        }

        public async Task<QuickEntry?> SuggestAsync(int transactionId)
        {
            var db = await database.GetConnectionAsync();
            var saved = await db.FindAsync<Transaction>(transactionId);
            if (saved is not { AccountId: { } accountId, TripId: null, SplitId: null } || saved.Type == TransactionType.Transfer
                || saved.AmountCents <= 0)
                return null;

            // Same payee (or none) + category + account + amount + type, counted in SQL
            var since = clock.GetLocalNow().DateTime.AddDays(-SuggestionDays);
            var repeats = saved.PayeeId is { } payeeId
                ? await db.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM \"Transaction\" WHERE Type = ? AND CategoryId = ? AND AccountId = ? AND AmountCents = ? AND PayeeId = ? AND Date >= ?",
                    (int)saved.Type, saved.CategoryId, accountId, saved.AmountCents, payeeId, since)
                : await db.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM \"Transaction\" WHERE Type = ? AND CategoryId = ? AND AccountId = ? AND AmountCents = ? AND PayeeId IS NULL AND Date >= ?",
                    (int)saved.Type, saved.CategoryId, accountId, saved.AmountCents, since);
            if (repeats < SuggestionRepeats)
                return null;

            var covered = await db.Table<QuickEntry>().Where(e =>
                e.Type == saved.Type && e.CategoryId == saved.CategoryId && e.AccountId == accountId
                && (e.AmountCents == saved.AmountCents || e.AmountCents == 0)).CountAsync();
            return covered > 0 ? null : await DraftFromTransactionAsync(transactionId);
        }

        public async Task DeleteAsync(int id)
        {
            var db = await database.GetConnectionAsync();
            await db.DeleteAsync<QuickEntry>(id);
        }

        public async Task MoveAsync(int id, bool up)
        {
            await database.RunInTransactionAsync(db =>
            {
                var all = db.Table<QuickEntry>().ToList().OrderBy(e => e.SortOrder).ThenBy(e => e.Id).ToList();
                var index = all.FindIndex(e => e.Id == id);
                var other = up ? index - 1 : index + 1;
                if (index < 0 || other < 0 || other >= all.Count)
                    return;

                (all[index], all[other]) = (all[other], all[index]);
                for (var i = 0; i < all.Count; i++)
                {
                    if (all[i].SortOrder != i)
                    {
                        all[i].SortOrder = i;
                        db.Update(all[i]);
                    }
                }
            });
        }

        public async Task<Transaction> UseAsync(int id, long? amountCents)
        {
            Transaction? saved = null;
            await database.RunInTransactionAsync(db =>
            {
                var entry = db.Find<QuickEntry>(id) ?? throw new InvalidOperationException($"Quick entry {id} does not exist.");
                var category = db.Find<Category>(entry.CategoryId)
                    ?? throw new InvalidOperationException($"Category {entry.CategoryId} does not exist.");
                if (db.Find<Account>(entry.AccountId) == null)
                    throw new InvalidOperationException($"Account {entry.AccountId} does not exist.");
                var cents = amountCents ?? entry.AmountCents;
                if (cents <= 0)
                    throw new InvalidOperationException("The quick entry needs an amount.");

                var now = clock.GetLocalNow().DateTime;
                saved = new Transaction
                {
                    Date = now,
                    Description = EntryDescription.Resolve(entry.Description, category.Name),
                    AmountCents = cents,
                    CategoryId = entry.CategoryId,
                    Type = entry.Type,
                    AccountId = entry.AccountId,
                    PayeeId = entry.PayeeId is { } payeeId && db.Find<Payee>(payeeId) != null ? payeeId : null
                };
                db.Insert(saved);

                entry.UseCount++;
                entry.LastUsedDate = now;
                db.Update(entry);
            });
            return saved!;
        }
    }
}
