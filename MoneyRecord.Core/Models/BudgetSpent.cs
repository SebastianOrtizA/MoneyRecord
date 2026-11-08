namespace MoneyRecord.Models
{
    /// <summary>
    /// A budget category's spending in a period: what counts against the (base currency) limit, and what doesn't
    /// (other currencies, or ones without a rate when converting), per currency.
    /// </summary>
    public sealed record BudgetSpent(decimal Counted, IReadOnlyList<CurrencyAmount> Uncounted)
    {
        public static readonly BudgetSpent None = new(0, []);

        /// <summary>What a budget spent: the overall budget (<see cref="Budget.AllSpending"/>) takes every category, any other its own.</summary>
        public static BudgetSpent For(IReadOnlyDictionary<int, BudgetSpent> byCategory, int categoryId, string baseCurrencyCode) =>
            categoryId == Budget.AllSpending
                ? Sum(byCategory.Values, baseCurrencyCode)
                : byCategory.GetValueOrDefault(categoryId) ?? None;

        public static BudgetSpent Sum(IEnumerable<BudgetSpent> parts, string baseCurrencyCode)
        {
            var list = parts.ToList();
            return new BudgetSpent(list.Sum(p => p.Counted), CurrencyAmount.Totals(list.SelectMany(p => p.Uncounted), baseCurrencyCode));
        }

        /// <summary>Without conversion: the base currency's amount counts, the others are listed.</summary>
        public static BudgetSpent FromCurrencies(IReadOnlyCollection<CurrencyAmount> spent, string baseCurrencyCode) =>
            new(CurrencyAmount.In(spent, baseCurrencyCode), CurrencyAmount.Except(spent, baseCurrencyCode));
    }
}
