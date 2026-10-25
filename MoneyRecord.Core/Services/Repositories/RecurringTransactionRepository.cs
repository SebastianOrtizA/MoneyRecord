using MoneyRecord.Models;

namespace MoneyRecord.Services.Repositories
{
    public sealed class RecurringTransactionRepository(DatabaseInitializer database)
        : RepositoryBase<RecurringTransaction>(database), Interfaces.IRecurringTransactionRepository
    {
        public async Task<List<RecurringTransaction>> GetActiveAsync() =>
            await (await TableAsync()).Where(r => r.IsActive).ToListAsync();

        public async Task<List<RecurringTransaction>> GetDueAsync(DateTime asOf) =>
            await (await TableAsync()).Where(r => r.IsActive && r.IsVariableAmount == false && r.NextOccurrence <= asOf).ToListAsync();

        public async Task<int> DeactivateAsync(int id)
        {
            var entity = await GetByIdAsync(id);
            if (entity == null)
                return 0;

            entity.IsActive = false;
            return await SaveAsync(entity);
        }
    }
}
