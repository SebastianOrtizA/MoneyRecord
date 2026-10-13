using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;

namespace MoneyRecord.Helpers
{
    /// <summary>Totals enriched transactions per category for the Spending and Income reports.</summary>
    public static class CategoryReportBuilder
    {
        /// <param name="categoryIds">
        /// Every category of the report's type: a category's colour slot is its place among them by ID (the first
        /// <see cref="CategoryAmount.PaletteSize"/> get one each, the rest share "Other"). A category not in the list
        /// (deleted since) is "Other" too.
        /// </param>
        public static CategoryReport Build(IEnumerable<Transaction> transactions, CategoryType type, IEnumerable<int> categoryIds)
        {
            var transactionType = type == CategoryType.Income ? TransactionType.Income : TransactionType.Expense;
            var matching = transactions.Where(t => t.Type == transactionType).ToList();
            var total = matching.Sum(t => Math.Abs(t.Amount));
            if (total == 0)
                return new CategoryReport(0, []);

            var slots = categoryIds.Distinct().Order()
                .Select((id, index) => (id, slot: index < CategoryAmount.PaletteSize ? index : CategoryAmount.OtherSlot))
                .ToDictionary(x => x.id, x => x.slot);

            var categories = matching
                .GroupBy(t => t.CategoryId)
                .Select(g =>
                {
                    var amount = g.Sum(t => Math.Abs(t.Amount));
                    var first = g.First();
                    return new CategoryAmount(
                        type,
                        first.CategoryId,
                        string.IsNullOrEmpty(first.CategoryName) ? AppResources.Unknown : first.CategoryName,
                        first.CategoryIconCode,
                        amount,
                        (double)(amount / total * 100),
                        slots.GetValueOrDefault(first.CategoryId, CategoryAmount.OtherSlot));
                })
                .OrderByDescending(c => c.Amount)
                .ToList();

            return new CategoryReport(total, categories);
        }
    }
}
