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

        /// <summary>The parts of a split payment (see <see cref="Transaction.SplitId"/>), in the order they were saved.</summary>
        Task<List<Transaction>> GetSplitPartsAsync(string splitId);

        /// <summary>Deletes every part of a split payment.</summary>
        Task DeleteSplitAsync(string splitId);

        /// <summary>
        /// Totals (unsigned) of one type in the range, one per currency (an account's; no account = the base currency),
        /// base currency first. Summed by SQLite; currencies are never added together.
        /// </summary>
        Task<List<CurrencyAmount>> SumByCurrencyAsync(TransactionType type, DateTime startDate, DateTime endDate,
            string baseCurrencyCode);

        /// <summary>Like <see cref="SumByCurrencyAsync"/> per category ID; categories without any are absent.</summary>
        Task<Dictionary<int, List<CurrencyAmount>>> SumByCategoryAsync(TransactionType type, DateTime startDate,
            DateTime endDate, string baseCurrencyCode);

        /// <summary>
        /// The range's totals for a conversion, summed by SQLite: per type, category, currency and day, plus the entries' own
        /// conversions into <paramref name="baseCurrencyCode"/> (one row per type and category). See <see cref="DailyTotal"/>.
        /// </summary>
        Task<List<DailyTotal>> SumByDayAsync(DateTime startDate, DateTime endDate, string baseCurrencyCode);

        /// <summary>The date of the first transaction in each currency (no account = the base currency).</summary>
        Task<Dictionary<string, DateTime>> GetFirstDatesByCurrencyAsync(string baseCurrencyCode);

        /// <summary>
        /// Totals of one type per account, with each account's latest date of that type: all-time (no <paramref name="since"/>),
        /// or only the entries dated on or after <paramref name="since"/>.
        /// </summary>
        Task<List<AccountTotal>> SumCentsByAccountAsync(TransactionType type, DateTime? since = null);
    }
}
