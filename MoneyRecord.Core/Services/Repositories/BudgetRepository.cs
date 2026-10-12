using MoneyRecord.Models;

namespace MoneyRecord.Services.Repositories
{
    public sealed class BudgetRepository(DatabaseInitializer database)
        : RepositoryBase<Budget>(database), Interfaces.IBudgetRepository
    {
        public async Task<List<Budget>> GetActiveBudgetsAsync() =>
            await (await TableAsync()).Where(b => b.IsActive).ToListAsync();

        public async Task<Budget?> GetByCategoryIdAsync(int categoryId) =>
            await (await TableAsync()).Where(b => b.CategoryId == categoryId && b.IsActive).FirstOrDefaultAsync();

        public async Task<int> UpdateAmountAsync(int budgetId, decimal newAmount)
        {
            var budget = await GetByIdAsync(budgetId);
            if (budget == null)
                return 0;

            budget.LimitAmount = newAmount;
            return await SaveAsync(budget);
        }
    }
}
