namespace MoneyRecord.Models
{
    /// <summary>
    /// A budget category's spending in a period: what counts against the (base currency) limit, and what doesn't
    /// (other currencies, or ones without a rate when converting), per currency.
    /// </summary>
    public sealed record BudgetSpent(decimal Counted, IReadOnlyList<CurrencyAmount> Uncounted)
    {
        public static readonly BudgetSpent None = new(0, []);

        /// <summary>Without conversion: the base currency's amount counts, the others are listed.</summary>
        public static BudgetSpent FromCurrencies(IReadOnlyCollection<CurrencyAmount> spent, string baseCurrencyCode) =>
            new(CurrencyAmount.In(spent, baseCurrencyCode), CurrencyAmount.Except(spent, baseCurrencyCode));
    }
}
