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

        /// <summary>The variable-amount occurrences due through today that wait to be entered or skipped, oldest first.</summary>
        Task<List<UpcomingEntry>> GetPendingAsync();

        /// <summary>
        /// Saves <paramref name="transaction"/> as the next occurrence of a variable-amount schedule: remembers its amount as the
        /// estimate and moves the schedule on. Refuses (<see cref="InvalidOperationException"/>) a schedule that is not
        /// variable or active, or a date that is not its next occurrence.
        /// </summary>
        Task EnterOccurrenceAsync(int recurringId, Transaction transaction);

        /// <summary>Moves a variable-amount schedule past its next occurrence without creating anything.</summary>
        Task SkipOccurrenceAsync(int recurringId);
    }
}
