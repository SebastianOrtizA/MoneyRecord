using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;

namespace MoneyRecord.Helpers
{
    /// <summary>Totals enriched transactions per category for the Spending and Income reports.</summary>
    public static class CategoryReportBuilder
    {
        /// <param name="categoryIds">
        /// Every category of the report's type. Which categories get their own colour depends on amount: the
        /// <see cref="CategoryAmount.PaletteSize"/> largest in this report, the rest fold into "Other". Which colour
        /// depends on the category: its place among these IDs (wrapping past the palette), so it stays the same when
        /// the period changes; only when a larger category already holds that colour does it take the next free one.
        /// </param>
        /// <param name="currencyCode">The currency of the transactions (one per report), kept on each amount for display.</param>
        public static CategoryReport Build(IEnumerable<Transaction> transactions, CategoryType type, IEnumerable<int> categoryIds,
            string currencyCode = "")
        {
            var transactionType = type == CategoryType.Income ? TransactionType.Income : TransactionType.Expense;
            var matching = transactions.Where(t => t.Type == transactionType).ToList();
            var total = matching.Sum(t => Math.Abs(t.Amount));
            if (total == 0)
                return new CategoryReport(0, []);

            var idIndex = categoryIds.Distinct().Order().Select((id, index) => (id, index)).ToDictionary(x => x.id, x => x.index);

            var groups = matching
                .GroupBy(t => t.CategoryId)
                .Select(g => (first: g.First(), amount: g.Sum(t => Math.Abs(t.Amount))))
                .OrderByDescending(g => g.amount)
                .ThenBy(g => g.first.CategoryId)
                .ToList();

            var taken = new bool[CategoryAmount.PaletteSize];
            var categories = groups
                .Select((g, rank) => new CategoryAmount(
                    type,
                    g.first.CategoryId,
                    string.IsNullOrEmpty(g.first.CategoryName) ? AppResources.Unknown : g.first.CategoryName,
                    g.first.CategoryIconCode,
                    g.amount,
                    (double)(g.amount / total * 100),
                    rank < CategoryAmount.PaletteSize
                        ? TakeSlot(taken, idIndex.GetValueOrDefault(g.first.CategoryId, g.first.CategoryId))
                        : CategoryAmount.OtherSlot)
                    { CurrencyCode = currencyCode })
                .ToList();

            return new CategoryReport(total, categories);
        }

        /// <summary>The category's own slot (its ID position, wrapped) if free, else the next free one after it.</summary>
        private static int TakeSlot(bool[] taken, int preferred)
        {
            for (var step = 0; step < taken.Length; step++)
            {
                var slot = (Math.Abs(preferred) + step) % taken.Length;
                if (!taken[slot])
                {
                    taken[slot] = true;
                    return slot;
                }
            }

            return CategoryAmount.OtherSlot;
        }
    }
}
