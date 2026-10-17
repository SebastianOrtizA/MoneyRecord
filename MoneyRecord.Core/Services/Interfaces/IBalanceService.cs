using MoneyRecord.Models;

namespace MoneyRecord.Services.Interfaces
{
    /// <summary>
    /// Balances and totals. Period totals are unsigned sums per type, one per currency (base currency first): amounts in
    /// different currencies are never added together.
    /// </summary>
    public interface IBalanceService
    {
        Task<decimal> GetAccountBalanceAsync(int accountId);
        Task<List<CurrencyAmount>> GetTotalIncomesAsync(DateTime startDate, DateTime endDate);
        Task<List<CurrencyAmount>> GetTotalExpensesAsync(DateTime startDate, DateTime endDate);

        /// <summary>
        /// The start of tomorrow: balances count the entries dated before it (a future-dated one counts from its date on).
        /// Whatever works back from today's balances stops here too.
        /// </summary>
        DateTime CountedBefore { get; }

        /// <summary>Every account's balance today in its own currency and its latest activity (or creation) date.</summary>
        Task<List<AccountBalanceInfo>> GetAllAccountBalancesAsync();

        /// <summary>
        /// How much each account's balance changed through the transactions and transfers dated on or after <paramref name="since"/>
        /// and up to today (in its own currency); accounts without any are absent.
        /// </summary>
        Task<Dictionary<int, decimal>> GetChangesSinceAsync(DateTime since);

        /// <summary>Expenses in the range per category ID, in one query; categories without any are absent.</summary>
        Task<Dictionary<int, List<CurrencyAmount>>> GetExpensesByCategoryAsync(DateTime startDate, DateTime endDate);
    }
}
