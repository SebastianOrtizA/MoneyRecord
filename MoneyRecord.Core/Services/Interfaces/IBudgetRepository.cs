using MoneyRecord.Models;

namespace MoneyRecord.Services.Interfaces
{
    public interface IBudgetRepository : IRepository<Budget>
    {
        Task<List<Budget>> GetActiveBudgetsAsync();
        Task<Budget?> GetByCategoryIdAsync(int categoryId);
        /// <summary>Sets an existing budget's limit and period; 0 when it doesn't exist.</summary>
        Task<int> UpdateLimitAsync(int budgetId, decimal limitAmount, BudgetPeriod period);
        Task<int> DeleteByIdAsync(int budgetId);
    }
}
