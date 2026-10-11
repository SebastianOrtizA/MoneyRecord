using MoneyRecord.Models;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Services.Repositories
{
    /// <summary>
    /// SQLite implementation of Transfer repository.
    /// Single responsibility: Transfer CRUD operations.
    /// </summary>
    public sealed class TransferRepository : ITransferRepository
    {
        private readonly DatabaseInitializer _dbInitializer;

        public TransferRepository(DatabaseInitializer dbInitializer)
        {
            _dbInitializer = dbInitializer;
        }

        private async Task EnsureInitializedAsync()
        {
            await _dbInitializer.InitializeAsync();
        }

        public async Task<List<Transfer>> GetAllAsync()
        {
            await EnsureInitializedAsync();
            return await _dbInitializer.Database!.Table<Transfer>().ToListAsync() ?? [];
        }

        public async Task<Transfer?> GetByIdAsync(int id)
        {
            await EnsureInitializedAsync();
            return await _dbInitializer.Database!.Table<Transfer>()
                .Where(t => t.Id == id)
                .FirstOrDefaultAsync();
        }

        public async Task<List<Transfer>> GetByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            await EnsureInitializedAsync();
            return await _dbInitializer.Database!.Table<Transfer>()
                .Where(t => t.Date >= startDate && t.Date <= endDate)
                .ToListAsync() ?? [];
        }

        public async Task<List<Transfer>> GetBySourceAccountIdAsync(int accountId)
        {
            await EnsureInitializedAsync();
            return await _dbInitializer.Database!.Table<Transfer>()
                .Where(t => t.SourceAccountId == accountId)
                .ToListAsync();
        }

        public async Task<List<Transfer>> GetByDestinationAccountIdAsync(int accountId)
        {
            await EnsureInitializedAsync();
            return await _dbInitializer.Database!.Table<Transfer>()
                .Where(t => t.DestinationAccountId == accountId)
                .ToListAsync();
        }

        public async Task<List<Transfer>> GetByAccountIdAsync(int accountId)
        {
            await EnsureInitializedAsync();
            return await _dbInitializer.Database!.Table<Transfer>()
                .Where(t => t.SourceAccountId == accountId || t.DestinationAccountId == accountId)
                .ToListAsync();
        }

        public async Task<int> SaveAsync(Transfer entity)
        {
            await EnsureInitializedAsync();
            if (entity.Id != 0)
            {
                return await _dbInitializer.Database!.UpdateAsync(entity);
            }
            return await _dbInitializer.Database!.InsertAsync(entity);
        }

        public async Task<int> DeleteAsync(Transfer entity)
        {
            await EnsureInitializedAsync();
            return await _dbInitializer.Database!.DeleteAsync(entity);
        }
    }
}
