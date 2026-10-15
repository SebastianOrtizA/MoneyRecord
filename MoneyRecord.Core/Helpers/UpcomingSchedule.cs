using MoneyRecord.Models;

namespace MoneyRecord.Helpers
{
    /// <summary>
    /// The occurrences recurring transactions will create in the coming days, from each one's next occurrence and
    /// <see cref="RecurrenceCalculator"/> (the same rule that creates them), up to its end date.
    /// </summary>
    public static class UpcomingSchedule
    {
        /// <summary>How far ahead the Upcoming lists look: today and the next 30 days.</summary>
        public const int DefaultDays = 30;

        /// <summary>
        /// The occurrence days of <paramref name="recurring"/> from <paramref name="from"/> through
        /// <paramref name="through"/> (both days, inclusive). An inactive recurrence has none. Occurrences already due
        /// but not created yet (before <paramref name="from"/>) are skipped: the app creates them when it starts.
        /// </summary>
        public static IEnumerable<DateTime> Occurrences(RecurringTransaction recurring, DateTime from, DateTime through)
        {
            if (!recurring.IsActive)
                yield break;

            var date = recurring.NextOccurrence.Date;
            while (date <= through.Date && (recurring.EndDate is not { } end || date <= end))
            {
                if (date >= from.Date)
                    yield return date;
                date = RecurrenceCalculator.Next(date, recurring.Frequency, recurring.EffectiveDayOfMonth);
            }
        }

        /// <summary>
        /// Every occurrence from <paramref name="today"/> through <paramref name="days"/> days later, soonest first,
        /// with the category and account filled in.
        /// </summary>
        public static List<UpcomingEntry> Build(IEnumerable<RecurringTransaction> recurrings,
            IReadOnlyDictionary<int, Category> categories, IReadOnlyDictionary<int, Account> accounts,
            DateTime today, int days = DefaultDays)
        {
            today = today.Date;
            var entries = new List<UpcomingEntry>();

            foreach (var recurring in recurrings)
            {
                var category = categories.GetValueOrDefault(recurring.CategoryId);
                var account = accounts.GetValueOrDefault(recurring.AccountId);

                foreach (var date in Occurrences(recurring, today, today.AddDays(days)))
                {
                    entries.Add(new UpcomingEntry
                    {
                        RecurringId = recurring.Id,
                        Date = date,
                        DaysAway = (date - today).Days,
                        Description = recurring.Description,
                        Amount = recurring.Amount,
                        Type = recurring.TransactionType,
                        CategoryName = category?.Name ?? string.Empty,
                        CategoryIconCode = category?.IconCode ?? "F0770",
                        AccountName = account?.Name ?? string.Empty,
                        CurrencyCode = account?.CurrencyCode ?? string.Empty
                    });
                }
            }

            return entries.OrderBy(e => e.Date).ThenBy(e => e.Description, StringComparer.CurrentCulture).ToList();
        }
    }
}
