using MoneyRecord.Models;

namespace MoneyRecord.Helpers
{
    /// <summary>
    /// Converts amounts between currencies with downloaded daily rates (<see cref="ExchangeRate"/>): a day without its own
    /// rate uses the nearest earlier day's; a date before every rate can't be converted (null). Immutable once built.
    /// </summary>
    public sealed class CurrencyConverter
    {
        // Per (base, quote): dates ascending with their rates
        private readonly Dictionary<(string Base, string Quote), (DateTime[] Dates, double[] Rates)> _pairs;

        public CurrencyConverter(IEnumerable<ExchangeRate> rates)
        {
            _pairs = rates
                .Where(r => r.Rate > 0)
                .GroupBy(r => (Base: r.BaseCode.ToUpperInvariant(), Quote: r.QuoteCode.ToUpperInvariant()))
                .ToDictionary(g => g.Key, g =>
                {
                    var sorted = g.OrderBy(r => r.Date).ToArray();
                    return (sorted.Select(r => r.Date.Date).ToArray(), sorted.Select(r => r.Rate).ToArray());
                });
        }

        /// <summary>Some rates are stored (the "≈" choices are offered only then).</summary>
        public bool HasRates => _pairs.Count > 0;

        /// <summary>
        /// How many <paramref name="to"/> one <paramref name="from"/> is worth on <paramref name="date"/>: the stored pair,
        /// its inverse, or across a common base (rates downloaded for an earlier base currency still work). Null when none
        /// is known on or before that day.
        /// </summary>
        public decimal? Rate(string from, string to, DateTime date)
        {
            from = from.ToUpperInvariant();
            to = to.ToUpperInvariant();
            if (from == to)
                return 1m;

            if (Find(from, to, date) is { } direct)
                return direct;
            if (Find(to, from, date) is { } inverse)
                return 1m / inverse;

            // Both stored against the same base: from → base → to
            foreach (var pivot in _pairs.Keys.Select(k => k.Base).Distinct())
            {
                var fromRate = pivot == from ? 1m : Find(pivot, from, date);
                var toRate = pivot == to ? 1m : Find(pivot, to, date);
                if (fromRate is { } f && toRate is { } t)
                    return t / f;
            }

            return null;
        }

        /// <summary><paramref name="amount"/> in <paramref name="to"/>, rounded to the cent; null without a rate.</summary>
        public decimal? Convert(decimal amount, string from, DateTime date, string to) =>
            Rate(from, to, date) is { } rate ? Math.Round(amount * rate, 2, MidpointRounding.AwayFromZero) : null;

        /// <summary>
        /// A transaction's amount in <paramref name="to"/>: the user's own conversion when it is in that currency, else the
        /// downloaded rate of its date. <paramref name="accountCurrency"/> is its account's (resolved) currency.
        /// </summary>
        public decimal? Convert(Transaction transaction, string accountCurrency, string to)
        {
            if (transaction.ConvertedAmount is { } own
                && string.Equals(transaction.ConvertedCurrencyCode, to, StringComparison.OrdinalIgnoreCase))
                return Math.Abs(own);

            return Convert(Math.Abs(transaction.Amount), accountCurrency, transaction.Date, to);
        }

        /// <summary>The rate of the latest day on or before <paramref name="date"/>.</summary>
        private decimal? Find(string baseCode, string quoteCode, DateTime date)
        {
            if (!_pairs.TryGetValue((baseCode, quoteCode), out var pair))
                return null;

            var index = Array.BinarySearch(pair.Dates, date.Date);
            if (index < 0)
                index = ~index - 1; // the day before the insertion point
            return index >= 0 ? (decimal)pair.Rates[index] : null;
        }
    }
}
