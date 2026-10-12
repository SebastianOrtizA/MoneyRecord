using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;

namespace MoneyRecord.Helpers
{
    /// <summary>Totals enriched transactions per category for the Expense and Income reports.</summary>
    public static class CategoryReportBuilder
    {
        public static CategoryReport Build(IEnumerable<Transaction> transactions, CategoryType type)
        {
            var transactionType = type == CategoryType.Income ? TransactionType.Income : TransactionType.Expense;
            var matching = transactions.Where(t => t.Type == transactionType).ToList();
            var total = matching.Sum(t => Math.Abs(t.Amount));
            if (total == 0)
                return new CategoryReport(0, []);

            var groups = matching
                .GroupBy(t => t.CategoryId)
                .Select(g =>
                {
                    var amount = g.Sum(t => Math.Abs(t.Amount));
                    var first = g.First();
                    return (first, amount, percentage: (double)(amount / total * 100));
                })
                .OrderByDescending(g => g.percentage)
                .ToList();

            var max = groups[0].percentage;
            var min = groups[^1].percentage;

            var categories = groups
                .Select(g => new CategoryAmount(
                    type,
                    g.first.CategoryId,
                    string.IsNullOrEmpty(g.first.CategoryName) ? AppResources.Unknown : g.first.CategoryName,
                    g.first.CategoryIconCode,
                    g.amount,
                    g.percentage,
                    HeatLevel(g.percentage, min, max)))
                .ToList();

            return new CategoryReport(total, categories);
        }

        /// <summary>The share's position between the smallest and the largest share; the middle when they are all equal.</summary>
        public static int HeatLevel(double percentage, double min, double max)
        {
            var normalized = max > min ? (percentage - min) / (max - min) : 0.5;
            var level = (int)(normalized * (CategoryAmount.HeatLevels - 1));
            return Math.Clamp(level, 0, CategoryAmount.HeatLevels - 1);
        }
    }
}
