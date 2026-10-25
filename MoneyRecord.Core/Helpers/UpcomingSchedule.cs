using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;

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
        /// Every occurrence of <paramref name="recurring"/> not created yet, through <paramref name="through"/>
        /// (inclusive). That starts at its next occurrence, which can be today or earlier when it is already due (the app
        /// was left open past midnight, or the start date was in the past): those are still money that will move, and
        /// the app creates them when it starts. An inactive recurrence has none.
        /// </summary>
        public static IEnumerable<DateTime> Occurrences(RecurringTransaction recurring, DateTime through)
        {
            if (!recurring.IsActive)
                yield break;

            var date = recurring.NextOccurrence.Date;
            while (date <= through.Date && (recurring.EndDate is not { } end || date <= end))
            {
                yield return date;
                date = RecurrenceCalculator.Next(date, recurring.Frequency, recurring.EffectiveDayOfMonth);
            }
        }

        /// <summary>
        /// Every pending occurrence through <paramref name="days"/> days after <paramref name="today"/>, soonest first,
        /// with the category and account filled in. Overdue ones count as today. A debt's scheduled payments
        /// (<see cref="RecurringTransaction.DebtId"/>) are capped at what <paramref name="debtsLeft"/> says is left (debt ID →
        /// cents; none = nothing left), so they end with the debt; their amount includes the interest part.
        /// </summary>
        public static List<UpcomingEntry> Build(IEnumerable<RecurringTransaction> recurrings,
            IReadOnlyDictionary<int, Category> categories, IReadOnlyDictionary<int, Account> accounts,
            DateTime today, int days = DefaultDays, IReadOnlyDictionary<int, long>? debtsLeft = null)
        {
            today = today.Date;
            var entries = new List<UpcomingEntry>();

            foreach (var recurring in recurrings)
            {
                var category = categories.GetValueOrDefault(recurring.CategoryId);
                var account = accounts.GetValueOrDefault(recurring.AccountId);
                long? left = recurring.DebtId is { } debtId ? debtsLeft?.GetValueOrDefault(debtId) ?? 0 : null;

                foreach (var date in Occurrences(recurring, today.AddDays(days)))
                {
                    var variable = recurring.IsVariableAmount && recurring.DebtId == null;
                    var cents = variable ? recurring.LastAmountCents : recurring.AmountCents;
                    if (left != null)
                    {
                        if (left <= 0)
                            break;
                        cents = Math.Min(cents, left.Value);
                        left -= cents;
                        cents += recurring.InterestCents;
                    }

                    entries.Add(new UpcomingEntry
                    {
                        RecurringId = recurring.Id,
                        Date = date,
                        DaysAway = Math.Max(0, (date - today).Days),
                        Description = recurring.Description,
                        Amount = Money.FromCents(cents),
                        IsVariable = variable,
                        IsPending = variable && date <= today,
                        Type = recurring.TransactionType,
                        CategoryName = left != null ? AppResources.Debts : category?.Name ?? string.Empty,
                        CategoryIconCode = left != null ? DebtSummary.IconCode : category?.IconCode ?? IconHelper.DefaultCategoryIconCode,
                        AccountName = account?.Name ?? string.Empty,
                        CurrencyCode = account?.CurrencyCode ?? string.Empty
                    });
                }
            }

            return entries.OrderBy(e => e.Date).ThenBy(e => e.Description, StringComparer.CurrentCulture).ToList();
        }
    }
}
