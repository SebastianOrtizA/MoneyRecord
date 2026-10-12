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

        /// <summary>Total (unsigned, in cents) of one type in the range, optionally of one category. Summed by SQLite.</summary>
        Task<long> SumCentsAsync(TransactionType type, DateTime startDate, DateTime endDate, int? categoryId = null);

        /// <summary>Totals (unsigned, in cents) of one type in the range per category ID; categories without any are absent.</summary>
        Task<Dictionary<int, long>> SumCentsByCategoryAsync(TransactionType type, DateTime startDate, DateTime endDate);

        /// <summary>All-time totals of one type per account, with each account's latest date of that type.</summary>
        Task<List<AccountTotal>> SumCentsByAccountAsync(TransactionType type);
    }
}
