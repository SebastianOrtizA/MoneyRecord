using MoneyRecord.Models;

namespace MoneyRecord.Services.Interfaces
{
    public interface IBudgetRepository : IRepository<Budget>
    {
        Task<List<Budget>> GetActiveBudgetsAsync();
        Task<Budget?> GetByCategoryIdAsync(int categoryId);
        Task<int> UpdateAmountAsync(int budgetId, decimal newAmount);
        Task<int> DeleteByIdAsync(int budgetId);
    }
}
