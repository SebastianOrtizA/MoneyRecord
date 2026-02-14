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
        Task<bool> HasTransactionsAsync(int accountId);
        Task<bool> HasTransfersAsync(int accountId);
    }
}
