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

        /// <summary>Every account's balance in its own currency and its latest activity (or creation) date.</summary>
        Task<List<AccountBalanceInfo>> GetAllAccountBalancesAsync();

        /// <summary>
        /// How much each account's balance changed through the transactions and transfers dated after <paramref name="after"/>
        /// (in its own currency); accounts without any are absent.
        /// </summary>
        Task<Dictionary<int, decimal>> GetChangesAfterAsync(DateTime after);

        /// <summary>Expenses in the range per category ID, in one query; categories without any are absent.</summary>
        Task<Dictionary<int, List<CurrencyAmount>>> GetExpensesByCategoryAsync(DateTime startDate, DateTime endDate);
    }
}
