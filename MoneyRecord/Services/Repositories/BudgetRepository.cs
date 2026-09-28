using MoneyRecord.Models;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Services.Repositories
{
    public sealed class BudgetRepository : IBudgetRepository
    {
        private readonly DatabaseInitializer _dbInitializer;

        public BudgetRepository(DatabaseInitializer dbInitializer)
        {
            _dbInitializer = dbInitializer;
        }

        private async Task EnsureInitializedAsync()
        {
            await _dbInitializer.InitializeAsync();
        }

        public async Task<List<Budget>> GetAllAsync()
        {
            await EnsureInitializedAsync();
            return await _dbInitializer.Database!.Table<Budget>().ToListAsync() ?? [];
        }

        public async Task<Budget?> GetByIdAsync(int id)
        {
            await EnsureInitializedAsync();
            return await _dbInitializer.Database!.Table<Budget>()
                .Where(b => b.Id == id)
                .FirstOrDefaultAsync();
        }

        public async Task<List<Budget>> GetActiveBudgetsAsync()
        {
            await EnsureInitializedAsync();
            return await _dbInitializer.Database!.Table<Budget>()
                .Where(b => b.IsActive)
                .ToListAsync() ?? [];
        }

        public async Task<Budget?> GetByCategoryIdAsync(int categoryId)
        {
            await EnsureInitializedAsync();
            return await _dbInitializer.Database!.Table<Budget>()
                .Where(b => b.CategoryId == categoryId && b.IsActive)
                .FirstOrDefaultAsync();
        }

        public async Task<int> SaveAsync(Budget entity)
        {
            await EnsureInitializedAsync();
            if (entity.Id != 0)
            {
                return await _dbInitializer.Database!.UpdateAsync(entity);
            }
            return await _dbInitializer.Database!.InsertAsync(entity);
        }

        public async Task<int> UpdateAmountAsync(int budgetId, decimal newAmount)
        {
            await EnsureInitializedAsync();
            var budget = await _dbInitializer.Database!.Table<Budget>()
                .Where(b => b.Id == budgetId)
                .FirstOrDefaultAsync();
            if (budget != null)
            {
                budget.LimitAmount = newAmount;
                return await _dbInitializer.Database!.UpdateAsync(budget);
            }
            return 0;
        }

        public async Task<int> DeleteAsync(Budget entity)
        {
            await EnsureInitializedAsync();
            return await _dbInitializer.Database!.DeleteAsync(entity);
        }

        public async Task<int> DeleteByIdAsync(int budgetId)
        {
            await EnsureInitializedAsync();
            var budget = await _dbInitializer.Database!.Table<Budget>()
                .Where(b => b.Id == budgetId)
                .FirstOrDefaultAsync();
            if (budget != null)
            {
                return await _dbInitializer.Database!.DeleteAsync(budget);
            }
            return 0;
        }
    }
}
