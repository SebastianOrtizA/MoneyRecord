namespace MoneyRecord.Helpers
{
    /// <summary>Where a forecast event comes from.</summary>
    public enum ForecastSource
    {
        /// <summary>An occurrence of a recurring transaction.</summary>
        Recurring,

        /// <summary>A one-off entry dated after today (stored, but in no balance before its date).</summary>
        Scheduled,

        /// <summary>A scheduled payment on a debt, already capped at what is left.</summary>
        Debt
    }

    /// <summary>
    /// Money that will move on <paramref name="Date"/> in an account: <paramref name="Cents"/> is signed (negative leaves the
    /// account). An estimate is a variable-amount occurrence worth its last amount.
    /// </summary>
    public sealed record ForecastEvent(DateTime Date, int AccountId, long Cents, ForecastSource Source, bool IsEstimate,
        string Description = "", int? RecurringId = null);

    /// <summary>The balance (cents) at the end of a day.</summary>
    public sealed record ForecastPoint(DateTime Date, long Cents);

    /// <summary>One balance over time: a point per day from today through the horizon.</summary>
    public sealed record ForecastSeries(IReadOnlyList<ForecastPoint> Points)
    {
        /// <summary>The lowest balance and its first day.</summary>
        public ForecastPoint Lowest => Points.Aggregate((low, p) => p.Cents < low.Cents ? p : low);

        /// <summary>The balance on the last day.</summary>
        public long EndCents => Points[^1].Cents;

        /// <summary>The first day the balance is below zero; null when it never is.</summary>
        public DateTime? FirstNegativeDate => Points.FirstOrDefault(p => p.Cents < 0)?.Date;

        /// <summary>The day-by-day sum of series over the same days (one currency's accounts); needs at least one.</summary>
        public static ForecastSeries Combine(IReadOnlyCollection<ForecastSeries> series) =>
            series.Count == 0 ? throw new ArgumentException("At least one series is needed", nameof(series)) : new(series.First().Points.Select((p, i) => new ForecastPoint(p.Date, series.Sum(s => s.Points[i].Cents))).ToList());
    }

    /// <summary>
    /// Projects balances forward by laying known events over today's balances. Pure: the events come from the recurring
    /// transactions and future-dated entries; anything dated today or earlier is already in the start balance, so the
    /// caller passes only what is still to move (an overdue occurrence counts on today).
    /// </summary>
    public static class BalanceForecast
    {
        /// <summary>
        /// A series per account in <paramref name="startBalances"/> (account ID → cents today): today plus
        /// <paramref name="horizonDays"/> days. Events for other accounts or past the horizon are ignored.
        /// </summary>
        public static Dictionary<int, ForecastSeries> Build(IReadOnlyDictionary<int, long> startBalances, DateTime today,
            int horizonDays, IEnumerable<ForecastEvent> events)
        {
            today = today.Date;
            var perDay = new Dictionary<int, long[]>();
            foreach (var id in startBalances.Keys)
                perDay[id] = new long[horizonDays + 1];

            foreach (var e in events)
            {
                if (!perDay.TryGetValue(e.AccountId, out var days))
                    continue;
                var index = Math.Max(0, (e.Date.Date - today).Days);
                if (index <= horizonDays)
                    days[index] += e.Cents;
            }

            return startBalances.ToDictionary(kv => kv.Key, kv =>
            {
                var balance = kv.Value;
                var points = new List<ForecastPoint>(horizonDays + 1);
                for (var i = 0; i <= horizonDays; i++)
                {
                    balance += perDay[kv.Key][i];
                    points.Add(new ForecastPoint(today.AddDays(i), balance));
                }

                return new ForecastSeries(points);
            });
        }
    }
}
