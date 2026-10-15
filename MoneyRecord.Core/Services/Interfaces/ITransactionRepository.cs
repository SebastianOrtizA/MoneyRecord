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

        /// <summary>
        /// Totals (unsigned) of one type in the range, one per currency (an account's; no account = the base currency),
        /// base currency first. Summed by SQLite; currencies are never added together.
        /// </summary>
        Task<List<CurrencyAmount>> SumByCurrencyAsync(TransactionType type, DateTime startDate, DateTime endDate,
            string baseCurrencyCode);

        /// <summary>Like <see cref="SumByCurrencyAsync"/> per category ID; categories without any are absent.</summary>
        Task<Dictionary<int, List<CurrencyAmount>>> SumByCategoryAsync(TransactionType type, DateTime startDate,
            DateTime endDate, string baseCurrencyCode);

        /// <summary>All-time totals of one type per account, with each account's latest date of that type.</summary>
        Task<List<AccountTotal>> SumCentsByAccountAsync(TransactionType type);
    }
}
