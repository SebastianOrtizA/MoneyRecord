using MoneyRecord.Helpers;
using MoneyRecord.Models;

namespace MoneyRecord.Services.Interfaces
{
    /// <summary>What a processing run did: the occurrences created, and the debts its scheduled payments paid off.</summary>
    public sealed record RecurringRun(int Created, IReadOnlyList<int> PaidOffDebtIds)
    {
        public static readonly RecurringRun None = new(0, []);
    }

    public interface IRecurringTransactionService
    {
        Task<RecurringRun> ProcessDueTransactionsAsync();

        /// <summary>
        /// The occurrences the active recurring transactions will create from today through <paramref name="days"/>
        /// days later, soonest first (Home's Upcoming card, the Recurring page).
        /// </summary>
        Task<List<UpcomingEntry>> GetUpcomingAsync(int days = UpcomingSchedule.DefaultDays);
    }
}
