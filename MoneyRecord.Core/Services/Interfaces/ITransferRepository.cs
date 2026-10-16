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

        /// <summary>
        /// Totals leaving each account (unsigned, in cents), with its latest outgoing transfer date: all-time, or only the
        /// transfers dated on or after <paramref name="since"/>.
        /// </summary>
        Task<List<AccountTotal>> SumCentsBySourceAsync(DateTime? since = null);

        /// <summary>
        /// Totals arriving in each account (unsigned destination amounts, in cents), with its latest incoming
        /// transfer date (all-time, or from <paramref name="since"/> on).
        /// </summary>
        Task<List<AccountTotal>> SumCentsByDestinationAsync(DateTime? since = null);
    }
}
