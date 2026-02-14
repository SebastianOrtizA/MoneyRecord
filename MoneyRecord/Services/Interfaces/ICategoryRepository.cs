using MoneyRecord.Models;

namespace MoneyRecord.Services.Interfaces
{
    /// <summary>
    /// Repository interface for Category operations.
    /// Extends base repository with category-specific queries.
    /// </summary>
    public interface ICategoryRepository : IRepository<Category>
    {
        Task<List<Category>> GetByTypeAsync(CategoryType type);
        Task<bool> HasTransactionsAsync(int categoryId);
        Task<int> GetTransactionCountAsync(int categoryId);
    }
}
