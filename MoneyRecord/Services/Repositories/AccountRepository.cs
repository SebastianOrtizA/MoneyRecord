using MoneyRecord.Models;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Services.Repositories
{
    public sealed class AccountRepository : IAccountRepository
    {
        private readonly DatabaseInitializer _dbInitializer;
        private List<Account>? _cache;

        public AccountRepository(DatabaseInitializer dbInitializer)
        {
            _dbInitializer = dbInitializer;
        }

        private async Task EnsureInitializedAsync()
        {
            await _dbInitializer.InitializeAsync();
        }

        private void InvalidateCache()
        {
            _cache = null;
        }

        public async Task<List<Account>> GetAllAsync()
        {
            if (_cache != null)
                return _cache;

            await EnsureInitializedAsync();
            _cache = await _dbInitializer.Database!.Table<Account>().ToListAsync() ?? [];
            return _cache;
        }

        public async Task<Account?> GetByIdAsync(int id)
        {
            var all = await GetAllAsync();
            return all.FirstOrDefault(a => a.Id == id);
        }

        public async Task<Account?> GetDefaultAsync()
        {
            var all = await GetAllAsync();
            return all.FirstOrDefault(a => a.IsDefault);
        }

        public async Task<int> SaveAsync(Account entity)
        {
            await EnsureInitializedAsync();
            int result;
            if (entity.Id != 0)
            {
                result = await _dbInitializer.Database!.UpdateAsync(entity);
            }
            else
            {
                result = await _dbInitializer.Database!.InsertAsync(entity);
            }
            InvalidateCache();
            return result;
        }

        public async Task<int> DeleteAsync(Account entity)
        {
            await EnsureInitializedAsync();
            var result = await _dbInitializer.Database!.DeleteAsync(entity);
            InvalidateCache();
            return result;
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
