using MoneyRecord.Models;

namespace MoneyRecord.Helpers
{
    /// <summary>
    /// Occurrence dates of recurring transactions. One rule everywhere: occurrences are dates at 00:00, so a recurring
    /// transaction is due from the start of its day.
    /// </summary>
    public static class RecurrenceCalculator
    {
        /// <summary>
        /// The occurrence after <paramref name="current"/>. A monthly recurrence on a <paramref name="dayOfMonth"/> uses
        /// that day, or the month's last day when it is shorter (31 → 30 April, 28/29 February).
        /// </summary>
        public static DateTime Next(DateTime current, RecurrenceFrequency frequency, int? dayOfMonth = null)
        {
            var date = current.Date;

            if (frequency == RecurrenceFrequency.Monthly && dayOfMonth.HasValue)
                return OnDay(date.AddMonths(1), dayOfMonth.Value);

            return frequency switch
            {
                RecurrenceFrequency.Daily => date.AddDays(1),
                RecurrenceFrequency.Weekly => date.AddDays(7),
                RecurrenceFrequency.Biweekly => date.AddDays(14),
                RecurrenceFrequency.Yearly => date.AddYears(1),
                _ => date.AddMonths(1)
            };
        }

        /// <summary>
        /// The first occurrence after <paramref name="now"/> when a schedule restarts (an edited recurrence that was
        /// already due): this month's <paramref name="dayOfMonth"/> if still ahead, otherwise one period from today.
        /// </summary>
        public static DateTime NextAfter(DateTime now, RecurrenceFrequency frequency, int? dayOfMonth = null)
        {
            if (frequency == RecurrenceFrequency.Monthly && dayOfMonth.HasValue)
            {
                var thisMonth = OnDay(now.Date, dayOfMonth.Value);
                return thisMonth > now ? thisMonth : Next(thisMonth, frequency, dayOfMonth);
            }

            return Next(now, frequency, dayOfMonth);
        }

        private static DateTime OnDay(DateTime monthDate, int dayOfMonth)
        {
            var day = Math.Clamp(dayOfMonth, 1, DateTime.DaysInMonth(monthDate.Year, monthDate.Month));
            return new DateTime(monthDate.Year, monthDate.Month, day);
        }
    }
}
