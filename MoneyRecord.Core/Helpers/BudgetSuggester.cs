using MoneyRecord.Models;

namespace MoneyRecord.Helpers
{
    /// <summary>A proposed monthly limit for a category that has none.</summary>
    public sealed record BudgetSuggestion(int CategoryId, string Name, string IconCode, decimal Amount);

    /// <summary>Proposes monthly budgets from what the last months' spending in each category averaged.</summary>
    public static class BudgetSuggester
    {
        /// <summary>How many full months before the current one are averaged.</summary>
        public const int Months = 3;

        /// <summary>A round limit near the average: whole units, tens from 100 up. Months without spending count as zero.</summary>
        public static decimal Round(decimal average) =>
            average >= 100 ? Math.Round(average / 10, MidpointRounding.AwayFromZero) * 10 : Math.Round(average, MidpointRounding.AwayFromZero);

        /// <param name="candidates">The expense categories without a budget.</param>
        /// <param name="months">Each earlier month's spending per category (counted, base currency).</param>
        /// <returns>The categories that averaged something, biggest first.</returns>
        public static List<BudgetSuggestion> Suggest(IEnumerable<Category> candidates, IReadOnlyCollection<IReadOnlyDictionary<int, BudgetSpent>> months) =>
            candidates
                .Select(c => new BudgetSuggestion(c.Id, c.Name, c.IconCode, Round(months.Sum(m => m.GetValueOrDefault(c.Id)?.Counted ?? 0) / Months)))
                .Where(s => s.Amount > 0)
                .OrderByDescending(s => s.Amount)
                .ThenBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
    }
}
