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
        /// transfers dated after <paramref name="after"/>.
        /// </summary>
        Task<List<AccountTotal>> SumCentsBySourceAsync(DateTime after = default);

        /// <summary>
        /// Totals arriving in each account (unsigned destination amounts, in cents), with its latest incoming
        /// transfer date (all-time, or after <paramref name="after"/>).
        /// </summary>
        Task<List<AccountTotal>> SumCentsByDestinationAsync(DateTime after = default);
    }
}
