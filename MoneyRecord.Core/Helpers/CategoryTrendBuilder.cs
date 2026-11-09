using MoneyRecord.Models;

namespace MoneyRecord.Helpers
{
    /// <summary>One month of a category's spending (or income), unsigned.</summary>
    public sealed record CategoryMonth(DateTime Month, decimal Amount)
    {
        /// <summary>"Oct" / "oct", for a chart axis.</summary>
        public string Label => DateFormatter.Format(Month, DateStyle.Month);
    }

    /// <summary>
    /// A category over the last months. The average, the highest and the lowest month use the complete months (all but the
    /// last, which is still going on, unless it is the only one); <see cref="Current"/> is the last month so far.
    /// </summary>
    public sealed record CategoryTrend(IReadOnlyList<CategoryMonth> Months)
    {
        public decimal Total => Months.Sum(m => m.Amount);

        public CategoryMonth Current => Months[^1];

        private IReadOnlyList<CategoryMonth> Complete => Months.Count > 1 ? Months.Take(Months.Count - 1).ToList() : Months;

        public decimal Average => Complete.Count == 0 ? 0 : Complete.Sum(m => m.Amount) / Complete.Count;

        public CategoryMonth Highest => Complete.MaxBy(m => m.Amount)!;

        public CategoryMonth Lowest => Complete.MinBy(m => m.Amount)!;

        /// <summary>The last month so far against the average of the complete ones before it.</summary>
        public PeriodDelta CurrentVsAverage => new(Current.Amount, Average);

        public bool HasData => Total > 0;
    }

    /// <summary>Totals one category's transactions per calendar month.</summary>
    public static class CategoryTrendBuilder
    {
        /// <param name="transactions">In the report's currency (unsigned amounts); other types and categories are ignored.</param>
        public static CategoryTrend Build(IEnumerable<Transaction> transactions, TransactionType type, int categoryId,
            DateTime firstMonth, int months)
        {
            firstMonth = new DateTime(firstMonth.Year, firstMonth.Month, 1);
            var byMonth = transactions
                .Where(t => t.Type == type && t.CategoryId == categoryId)
                .GroupBy(t => new DateTime(t.Date.Year, t.Date.Month, 1))
                .ToDictionary(g => g.Key, g => g.Sum(t => Math.Abs(t.Amount)));

            return new CategoryTrend(Enumerable.Range(0, Math.Max(months, 1))
                .Select(i => firstMonth.AddMonths(i))
                .Select(month => new CategoryMonth(month, byMonth.GetValueOrDefault(month)))
                .ToList());
        }
    }
}
