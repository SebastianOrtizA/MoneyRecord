using MoneyRecord.Models;

namespace MoneyRecord.Helpers
{
    /// <summary>One category of the comparison: its total in period A and in period B (unsigned) and the change of A against B.</summary>
    public sealed record CompareRow(int CategoryId, string Name, string IconCode, decimal AmountA, decimal AmountB)
    {
        public PeriodDelta Delta => new(AmountA, AmountB);
    }

    public sealed record PeriodCompare(decimal TotalA, decimal TotalB, IReadOnlyList<CompareRow> Rows)
    {
        public bool HasData => TotalA != 0 || TotalB != 0;

        public PeriodDelta Delta => new(TotalA, TotalB);
    }

    /// <summary>Two periods side by side per category (spending or income): A is the one looked at, B the one it is compared with.</summary>
    public static class PeriodCompareBuilder
    {
        /// <param name="unknownName">Label of entries whose category has no name.</param>
        /// <remarks>A category found in only one period is a row with 0 in the other. Rows are ordered by the larger of the two amounts.</remarks>
        public static PeriodCompare Build(IEnumerable<Transaction> periodA, IEnumerable<Transaction> periodB, TransactionType type,
            string unknownName)
        {
            var a = Totals(periodA, type);
            var b = Totals(periodB, type);

            var rows = a.Keys.Union(b.Keys)
                .Select(id =>
                {
                    var first = a.TryGetValue(id, out var fa) ? fa : b[id];
                    var name = string.IsNullOrEmpty(first.Name) ? unknownName : first.Name;
                    return new CompareRow(id, name, first.Icon, a.TryGetValue(id, out var ta) ? ta.Amount : 0,
                        b.TryGetValue(id, out var tb) ? tb.Amount : 0);
                })
                .OrderByDescending(r => Math.Max(r.AmountA, r.AmountB)).ThenBy(r => r.CategoryId)
                .ToList();

            return new PeriodCompare(rows.Sum(r => r.AmountA), rows.Sum(r => r.AmountB), rows);
        }

        private static Dictionary<int, (string Name, string Icon, decimal Amount)> Totals(IEnumerable<Transaction> transactions,
            TransactionType type) =>
            transactions.Where(t => t.Type == type)
                .GroupBy(t => t.CategoryId)
                .ToDictionary(g => g.Key, g => (g.First().CategoryName, g.First().CategoryIconCode, g.Sum(t => Math.Abs(t.Amount))));
    }

    /// <summary>The ready-made pairs of periods (A, B) of the compare page.</summary>
    public enum ComparePreset { ThisMonthVsLast, ThisYearVsLast, SameMonthLastYear, Last30Days }

    public static class ComparePresets
    {
        /// <returns>Whole days: start at midnight, end at the last tick of the day; A is the later period.</returns>
        public static ((DateTime Start, DateTime End) A, (DateTime Start, DateTime End) B) Resolve(ComparePreset preset, DateTime today)
        {
            static (DateTime, DateTime) Month(int year, int month)
            {
                var start = new DateTime(year, month, 1);
                return (start, start.AddMonths(1).AddTicks(-1));
            }

            static (DateTime, DateTime) Year(int year) => (new DateTime(year, 1, 1), new DateTime(year + 1, 1, 1).AddTicks(-1));

            var day = today.Date;
            switch (preset)
            {
                case ComparePreset.ThisYearVsLast:
                    return (Year(day.Year), Year(day.Year - 1));
                case ComparePreset.SameMonthLastYear:
                    return (Month(day.Year, day.Month), Month(day.Year - 1, day.Month));
                case ComparePreset.Last30Days:
                    var end = day.AddDays(1).AddTicks(-1);
                    return ((day.AddDays(-29), end), (day.AddDays(-59), day.AddDays(-29).AddTicks(-1)));
                default:
                    var last = day.AddMonths(-1);
                    return (Month(day.Year, day.Month), Month(last.Year, last.Month));
            }
        }
    }
}
