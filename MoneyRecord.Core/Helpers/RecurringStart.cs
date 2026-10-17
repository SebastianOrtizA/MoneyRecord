using MoneyRecord.Models;

namespace MoneyRecord.Helpers
{
    /// <summary>
    /// How a new recurring transaction starts. Dated today or earlier, its first entry is recorded now and the schedule
    /// goes on from the next period. Dated in the future, only the schedule is saved: its first occurrence is that date
    /// and nothing is recorded (or counted in a balance) before it.
    /// </summary>
    public sealed record RecurringStart(bool RecordsFirstNow, DateTime NextOccurrence, DateTime? LastProcessedDate)
    {
        /// <summary>The earliest first date of a schedule made from the Recurring page: tomorrow.</summary>
        public static DateTime EarliestScheduleDate(DateTime today) => today.Date.AddDays(1);

        public static bool IsFuture(DateTime date, DateTime today) => date.Date > today.Date;

        public static RecurringStart For(DateTime date, DateTime today, RecurrenceFrequency frequency, int? dayOfMonth) =>
            IsFuture(date, today)
                ? new RecurringStart(false, date.Date, null)
                : new RecurringStart(true, RecurrenceCalculator.Next(date, frequency, dayOfMonth), date);
    }
}
