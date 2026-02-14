using MoneyRecord.Models;

namespace MoneyRecord.Services.Interfaces
{
    /// <summary>
    /// Repository interface for Transaction operations.
    /// Extends base repository with transaction-specific queries.
    /// </summary>
    public interface ITransactionRepository : IRepository<Transaction>
    {
        Task<List<Transaction>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);
        Task<List<Transaction>> GetByAccountIdAsync(int accountId);
        Task<List<Transaction>> GetByCategoryIdAsync(int categoryId);
        Task<int> ReassignCategoryAsync(int fromCategoryId, int toCategoryId);
        Task<int> ReassignAccountAsync(int fromAccountId, int toAccountId);
    }
}
