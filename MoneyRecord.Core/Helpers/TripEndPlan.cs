using MoneyRecord.Models;

namespace MoneyRecord.Helpers
{
    /// <summary>One row of the End trip sheet: a trip category in use, and the real category it starts on (null = none yet).</summary>
    public sealed record TripEndRowPlan(TripCategory Source, Category? Target);

    /// <summary>What the End trip sheet offers: the real categories an expense can move to, and a row per trip category in use.</summary>
    public static class TripEndPlan
    {
        /// <summary>Expense categories except "Travel" (trip expenses never move back into it).</summary>
        public static List<Category> Targets(IEnumerable<Category> categories) =>
            categories.Where(c => c.Type == CategoryType.Expense && !c.IsTravel).ToList();

        /// <summary>
        /// A row per trip category the expenses use (in the trip categories' order), prefilled with the category it went to
        /// when a trip last ended, if that one is still offered.
        /// </summary>
        public static List<TripEndRowPlan> Rows(IEnumerable<Transaction> expenses, IEnumerable<TripCategory> tripCategories,
            IReadOnlyList<Category> targets)
        {
            var used = expenses.Select(e => e.TripCategoryId).OfType<int>().ToHashSet();
            return tripCategories
                .Where(c => used.Contains(c.Id))
                .Select(c => new TripEndRowPlan(c, targets.FirstOrDefault(t => t.Id == c.LastCategoryId)))
                .ToList();
        }
    }
}
