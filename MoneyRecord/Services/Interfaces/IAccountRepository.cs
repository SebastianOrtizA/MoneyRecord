using MoneyRecord.Models;

namespace MoneyRecord.Services.Interfaces
{
    /// <summary>
    /// Repository interface for Account operations.
    /// Extends base repository with account-specific queries.
    /// </summary>
    public interface IAccountRepository : IRepository<Account>
    {
        Task<Account?> GetDefaultAsync();

        /// <summary>
        /// Drops cached rows after another service changed the table directly (inside a multi-table transaction).
        /// </summary>
        void InvalidateCache();
    }
}
