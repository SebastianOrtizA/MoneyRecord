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

        /// <summary>All-time totals leaving each account (unsigned, in cents), with its latest outgoing transfer date.</summary>
        Task<List<AccountTotal>> SumCentsBySourceAsync();

        /// <summary>All-time totals arriving in each account (unsigned, in cents), with its latest incoming transfer date.</summary>
        Task<List<AccountTotal>> SumCentsByDestinationAsync();
    }
}
