using MoneyRecord.Models;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Services.Repositories
{
    /// <summary>
    /// SQLite implementation of Account repository.
    /// Single responsibility: Account CRUD operations.
    /// </summary>
    public sealed class AccountRepository : IAccountRepository
    {
        private readonly DatabaseInitializer _dbInitializer;

        public AccountRepository(DatabaseInitializer dbInitializer)
        {
            _dbInitializer = dbInitializer;
        }

        private async Task EnsureInitializedAsync()
        {
            await _dbInitializer.InitializeAsync();
        }

        public async Task<List<Account>> GetAllAsync()
        {
            await EnsureInitializedAsync();
            return await _dbInitializer.Database!.Table<Account>().ToListAsync() ?? [];
        }

        public async Task<Account?> GetByIdAsync(int id)
        {
            await EnsureInitializedAsync();
            return await _dbInitializer.Database!.Table<Account>()
                .Where(a => a.Id == id)
                .FirstOrDefaultAsync();
        }

        public async Task<Account?> GetDefaultAsync()
        {
            await EnsureInitializedAsync();
            return await _dbInitializer.Database!.Table<Account>()
                .Where(a => a.IsDefault)
                .FirstOrDefaultAsync();
        }

        public async Task<int> SaveAsync(Account entity)
        {
            await EnsureInitializedAsync();
            if (entity.Id != 0)
            {
                return await _dbInitializer.Database!.UpdateAsync(entity);
            }
            return await _dbInitializer.Database!.InsertAsync(entity);
        }

        public async Task<int> DeleteAsync(Account entity)
        {
            await EnsureInitializedAsync();
            return await _dbInitializer.Database!.DeleteAsync(entity);
        }

        public async Task<bool> HasTransactionsAsync(int accountId)
        {
            await EnsureInitializedAsync();
            var count = await _dbInitializer.Database!.Table<Transaction>()
                .Where(t => t.AccountId == accountId)
                .CountAsync();
            return count > 0;
        }

        public async Task<bool> HasTransfersAsync(int accountId)
        {
            await EnsureInitializedAsync();
            var count = await _dbInitializer.Database!.Table<Transfer>()
                .Where(t => t.SourceAccountId == accountId || t.DestinationAccountId == accountId)
                .CountAsync();
            return count > 0;
        }
    }
}
