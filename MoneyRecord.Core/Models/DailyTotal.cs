namespace MoneyRecord.Models
{
    /// <summary>
    /// One row of a converted total, summed by SQLite: a type and category's amount in one currency on one day (what a rate
    /// converts), or, with <see cref="IsOwnConversion"/>, the sum of the entries' own "Amount in {base}" (already in the
    /// base currency, any day). Mutable with a parameterless constructor because sqlite-net maps query rows onto it.
    /// </summary>
    public sealed class DailyTotal
    {
        public TransactionType Type { get; set; }

        public int CategoryId { get; set; }

        /// <summary>The account's currency (no account = the base); the base for an own conversion.</summary>
        public string CurrencyCode { get; set; } = string.Empty;

        /// <summary>Days since 0001-01-01 (the date's ticks / ticks per day); 0 for an own conversion.</summary>
        public long Day { get; set; }

        public long Cents { get; set; }

        public bool IsOwnConversion { get; set; }

        public DateTime Date => new(Day * TimeSpan.TicksPerDay);

        public decimal Amount => Helpers.Money.FromCents(Cents);
    }
}
