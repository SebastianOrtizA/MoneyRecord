namespace MoneyRecord.Helpers
{
    /// <summary>How far ahead "safe to spend" looks (Settings → Home screen).</summary>
    public enum SafeToSpendHorizon
    {
        /// <summary>Until the next income rule pays (end of month when there is none).</summary>
        NextIncome,

        /// <summary>Until the last day of the month.</summary>
        EndOfMonth
    }

    /// <summary>
    /// What can be spent: <paramref name="TotalCents"/> in all, <paramref name="PerDayCents"/> a day over <paramref name="Days"/>
    /// days; <paramref name="Until"/> is the day shown ("until Nov 14": the income day, or the last day of the month).
    /// </summary>
    public sealed record SafeToSpendResult(long TotalCents, long PerDayCents, DateTime Until, int Days)
    {
        public bool IsNegative => TotalCents < 0;
    }

    /// <summary>
    /// The available balance minus what is already committed (expense rules, pending estimates, debt payments, future-dated
    /// expenses) up to the horizon. Income that has not arrived is never counted, and budgets are intent, not commitments.
    /// </summary>
    public static class SafeToSpend
    {
        /// <summary>The day of the first income occurrence after today; null when none.</summary>
        public static DateTime? NextIncome(IEnumerable<ForecastEvent> events, DateTime today) =>
            events.Where(e => e.Cents > 0 && e.Source == ForecastSource.Recurring && e.Date.Date > today.Date)
                .Select(e => (DateTime?)e.Date.Date).Min();

        /// <param name="availableCents">What the accounts hold today (one currency).</param>
        /// <param name="events">The forecast events of those accounts (only outflows are used).</param>
        public static SafeToSpendResult Compute(long availableCents, IEnumerable<ForecastEvent> events, DateTime today,
            SafeToSpendHorizon horizon)
        {
            today = today.Date;
            var list = events as IReadOnlyCollection<ForecastEvent> ?? events.ToList();
            var income = horizon == SafeToSpendHorizon.NextIncome ? NextIncome(list, today) : null;
            var until = income ?? new DateTime(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));

            // The income day itself is covered by the income
            var last = income != null ? until.AddDays(-1) : until;
            var days = Math.Max(1, (last - today).Days + 1);
            var committed = list.Where(e => e.Cents < 0 && e.Date.Date <= last).Sum(e => e.Cents);
            var total = availableCents + committed;
            return new SafeToSpendResult(total, total / days, until, days);
        }
    }
}
