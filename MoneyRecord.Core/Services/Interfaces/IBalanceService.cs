using MoneyRecord.Models;

namespace MoneyRecord.Services.Interfaces
{
    /// <summary>
    /// Balances and totals. Amounts are unsigned sums per type; a total across accounts is in the base currency
    /// until exchange rates exist (Stage 10.3).
    /// </summary>
    public interface IBalanceService
    {
        Task<decimal> GetAccountBalanceAsync(int accountId);
        Task<decimal> GetTotalIncomesAsync(DateTime startDate, DateTime endDate);
        Task<decimal> GetTotalExpensesAsync(DateTime startDate, DateTime endDate);

        /// <summary>Every account's balance in its own currency and its latest activity (or creation) date.</summary>
        Task<List<AccountBalanceInfo>> GetAllAccountBalancesAsync();

        Task<decimal> GetCategoryExpensesAsync(int categoryId, DateTime startDate, DateTime endDate);

        /// <summary>Expenses in the range per category ID, in one query; categories without any are absent.</summary>
        Task<Dictionary<int, decimal>> GetExpensesByCategoryAsync(DateTime startDate, DateTime endDate);
    }
}
