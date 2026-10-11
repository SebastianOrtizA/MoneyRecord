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

        /// <summary>
        /// Drops cached rows after another service changed the table directly (inside a multi-table transaction).
        /// </summary>
        void InvalidateCache();
    }
}
