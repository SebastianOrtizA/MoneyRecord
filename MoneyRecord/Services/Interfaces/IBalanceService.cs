using MoneyRecord.Models;

namespace MoneyRecord.Services.Interfaces
{
    /// <summary>
    /// Service interface for balance calculations.
    /// Single responsibility: calculating financial balances.
    /// </summary>
    public interface IBalanceService
    {
        Task<decimal> GetTotalBalanceAsync();
        Task<decimal> GetAccountBalanceAsync(int accountId);
        Task<decimal> GetTotalIncomesAsync(DateTime startDate, DateTime endDate);
        Task<decimal> GetTotalExpensesAsync(DateTime startDate, DateTime endDate);
        Task<decimal> GetBalanceAsync(DateTime? startDate = null, DateTime? endDate = null);
        Task<List<AccountBalanceInfo>> GetAllAccountBalancesAsync();
        Task<DateTime?> GetLastTransactionDateForAccountAsync(int accountId);
    }
}
