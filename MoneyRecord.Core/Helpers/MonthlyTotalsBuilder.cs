using MoneyRecord.Models;

namespace MoneyRecord.Helpers
{
    /// <summary>One calendar month's income and expense totals (unsigned, in the base currency until Stage 10.3).</summary>
    public sealed record MonthTotal(DateTime Month, decimal Income, decimal Expense)
    {
        /// <summary>"Oct" / "oct", for a chart axis (one label per bar, so it stays short).</summary>
        public string Label => DateFormatter.Format(Month, DateStyle.Month);

        /// <summary>Income minus expense: the month's cash flow.</summary>
        public decimal Net => Income - Expense;
    }

    /// <summary>Totals transactions per calendar month for the Trends charts.</summary>
    public static class MonthlyTotalsBuilder
    {
        /// <summary>The last <paramref name="months"/> calendar months up to and including <paramref name="today"/>'s.</summary>
        public static (DateTime Start, DateTime End) Window(DateTime today, int months)
        {
            var currentMonth = new DateTime(today.Year, today.Month, 1);
            return (currentMonth.AddMonths(-(months - 1)), currentMonth.AddMonths(1).AddTicks(-1));
        }

        /// <summary>One row per month from <paramref name="firstMonth"/>, oldest first; months without entries are zero.</summary>
        public static List<MonthTotal> Build(IEnumerable<Transaction> transactions, DateTime firstMonth, int months)
        {
            firstMonth = new DateTime(firstMonth.Year, firstMonth.Month, 1);
            var byMonth = transactions
                .GroupBy(t => new DateTime(t.Date.Year, t.Date.Month, 1))
                .ToDictionary(g => g.Key, g => (
                    Income: g.Where(t => t.Type == TransactionType.Income).Sum(t => Math.Abs(t.Amount)),
                    Expense: g.Where(t => t.Type == TransactionType.Expense).Sum(t => Math.Abs(t.Amount))));

            return Enumerable.Range(0, months)
                .Select(i =>
                {
                    var month = firstMonth.AddMonths(i);
                    var totals = byMonth.GetValueOrDefault(month);
                    return new MonthTotal(month, totals.Income, totals.Expense);
                })
                .ToList();
        }
    }
}
