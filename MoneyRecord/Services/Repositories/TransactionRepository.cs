using MoneyRecord.Models;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Services.Repositories
{
    /// <summary>
    /// SQLite implementation of Transaction repository.
    /// Single responsibility: Transaction CRUD operations.
    /// </summary>
    public sealed class TransactionRepository : ITransactionRepository
    {
        private readonly DatabaseInitializer _dbInitializer;

        public TransactionRepository(DatabaseInitializer dbInitializer)
        {
            _dbInitializer = dbInitializer;
        }

        private async Task EnsureInitializedAsync()
        {
            await _dbInitializer.InitializeAsync();
        }

        public async Task<List<Transaction>> GetAllAsync()
        {
            await EnsureInitializedAsync();
            return await _dbInitializer.Database!.Table<Transaction>().ToListAsync();
        }

        public async Task<Transaction?> GetByIdAsync(int id)
        {
            await EnsureInitializedAsync();
            return await _dbInitializer.Database!.Table<Transaction>()
                .Where(t => t.Id == id)
                .FirstOrDefaultAsync();
        }

        public async Task<List<Transaction>> GetByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            await EnsureInitializedAsync();
            return await _dbInitializer.Database!.Table<Transaction>()
                .Where(t => t.Date >= startDate && t.Date <= endDate)
                .ToListAsync();
        }

        public async Task<List<Transaction>> GetByAccountIdAsync(int accountId)
        {
            await EnsureInitializedAsync();
            return await _dbInitializer.Database!.Table<Transaction>()
                .Where(t => t.AccountId == accountId)
                .ToListAsync();
        }

        public async Task<List<Transaction>> GetByCategoryIdAsync(int categoryId)
        {
            await EnsureInitializedAsync();
            return await _dbInitializer.Database!.Table<Transaction>()
                .Where(t => t.CategoryId == categoryId)
                .ToListAsync();
        }

        public async Task<int> SaveAsync(Transaction entity)
        {
            await EnsureInitializedAsync();
            if (entity.Id != 0)
            {
                return await _dbInitializer.Database!.UpdateAsync(entity);
            }
            return await _dbInitializer.Database!.InsertAsync(entity);
        }

        public async Task<int> DeleteAsync(Transaction entity)
        {
            await EnsureInitializedAsync();
            return await _dbInitializer.Database!.DeleteAsync(entity);
        }

        public async Task<int> ReassignCategoryAsync(int fromCategoryId, int toCategoryId)
        {
            await EnsureInitializedAsync();
            var transactions = await _dbInitializer.Database!.Table<Transaction>()
                .Where(t => t.CategoryId == fromCategoryId)
                .ToListAsync();

            foreach (var transaction in transactions)
            {
                transaction.CategoryId = toCategoryId;
                await _dbInitializer.Database.UpdateAsync(transaction);
            }

            return transactions.Count;
        }

        public async Task<int> ReassignAccountAsync(int fromAccountId, int toAccountId)
        {
            await EnsureInitializedAsync();
            var transactions = await _dbInitializer.Database!.Table<Transaction>()
                .Where(t => t.AccountId == fromAccountId)
                .ToListAsync();

            foreach (var transaction in transactions)
            {
                transaction.AccountId = toAccountId;
                await _dbInitializer.Database.UpdateAsync(transaction);
            }

            return transactions.Count;
        }
    }
}
