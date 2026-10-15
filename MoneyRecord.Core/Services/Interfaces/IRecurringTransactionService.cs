using MoneyRecord.Helpers;
using MoneyRecord.Models;

namespace MoneyRecord.Services.Interfaces
{
    public interface IRecurringTransactionService
    {
        Task<int> ProcessDueTransactionsAsync();

        /// <summary>
        /// The occurrences the active recurring transactions will create from today through <paramref name="days"/>
        /// days later, soonest first (Home's Upcoming card, the Recurring page).
        /// </summary>
        Task<List<UpcomingEntry>> GetUpcomingAsync(int days = UpcomingSchedule.DefaultDays);
    }
}
