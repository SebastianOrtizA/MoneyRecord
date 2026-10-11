using MoneyRecord.Models;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Services.Repositories
{
    public sealed class RecurringTransactionRepository : IRecurringTransactionRepository
    {
        private readonly DatabaseInitializer _dbInitializer;

        public RecurringTransactionRepository(DatabaseInitializer dbInitializer)
        {
            _dbInitializer = dbInitializer;
        }

        private async Task EnsureInitializedAsync()
        {
            await _dbInitializer.InitializeAsync();
        }

        public async Task<List<RecurringTransaction>> GetAllAsync()
        {
            await EnsureInitializedAsync();
            return await _dbInitializer.Database!.Table<RecurringTransaction>().ToListAsync();
        }

        public async Task<RecurringTransaction?> GetByIdAsync(int id)
        {
            await EnsureInitializedAsync();
            return await _dbInitializer.Database!.Table<RecurringTransaction>()
                .Where(r => r.Id == id)
                .FirstOrDefaultAsync();
        }

        public async Task<List<RecurringTransaction>> GetActiveAsync()
        {
            await EnsureInitializedAsync();
            return await _dbInitializer.Database!.Table<RecurringTransaction>()
                .Where(r => r.IsActive)
                .ToListAsync();
        }

        public async Task<List<RecurringTransaction>> GetDueAsync(DateTime asOf)
        {
            await EnsureInitializedAsync();
            return await _dbInitializer.Database!.Table<RecurringTransaction>()
                .Where(r => r.IsActive && r.NextOccurrence <= asOf)
                .ToListAsync();
        }

        public async Task<int> SaveAsync(RecurringTransaction entity)
        {
            await EnsureInitializedAsync();
            if (entity.Id != 0)
            {
                return await _dbInitializer.Database!.UpdateAsync(entity);
            }
            return await _dbInitializer.Database!.InsertAsync(entity);
        }

        public async Task<int> DeleteAsync(RecurringTransaction entity)
        {
            await EnsureInitializedAsync();
            return await _dbInitializer.Database!.DeleteAsync(entity);
        }

        public async Task<int> DeleteByIdAsync(int id)
        {
            await EnsureInitializedAsync();
            var entity = await GetByIdAsync(id);
            if (entity == null) return 0;
            return await _dbInitializer.Database!.DeleteAsync(entity);
        }

        public async Task<int> DeactivateAsync(int id)
        {
            await EnsureInitializedAsync();
            var entity = await GetByIdAsync(id);
            if (entity == null) return 0;
            entity.IsActive = false;
            return await _dbInitializer.Database!.UpdateAsync(entity);
        }
    }
}
