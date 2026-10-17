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
        /// The user's own accounts: every account but the debts' hidden ones (<see cref="Account.IsDebt"/>). What account lists,
        /// pickers and the balance card show; <see cref="IRepository{T}.GetAllAsync"/> (names, currencies) has both.
        /// </summary>
        Task<List<Account>> GetOwnAsync();

        /// <summary>
        /// Drops cached rows after another service changed the table directly (inside a multi-table transaction).
        /// </summary>
        void InvalidateCache();
    }
}
