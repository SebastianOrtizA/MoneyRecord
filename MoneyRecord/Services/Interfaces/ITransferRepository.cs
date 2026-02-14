using MoneyRecord.Models;

namespace MoneyRecord.Services.Interfaces
{
    /// <summary>
    /// Repository interface for Transfer operations.
    /// Extends base repository with transfer-specific queries.
    /// </summary>
    public interface ITransferRepository : IRepository<Transfer>
    {
        Task<List<Transfer>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);
        Task<List<Transfer>> GetBySourceAccountIdAsync(int accountId);
        Task<List<Transfer>> GetByDestinationAccountIdAsync(int accountId);
        Task<List<Transfer>> GetByAccountIdAsync(int accountId);
    }
}
