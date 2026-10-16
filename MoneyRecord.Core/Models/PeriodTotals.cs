using MoneyRecord.Helpers;

namespace MoneyRecord.Models
{
    /// <summary>A period's income, expense and net in one currency (one row of Home's totals).</summary>
    public sealed record PeriodTotals(string CurrencyCode, decimal Income, decimal Expense)
    {
        public decimal Net => Income - Expense;

        public bool IsNetNegative => Net < 0;

        /// <summary>Converted from other currencies with downloaded rates (the captions get a "≈").</summary>
        public bool IsApproximate { get; init; }

        /// <summary>"≈ " before a caption while approximate (on the caption, so a long amount keeps its line).</summary>
        public string ApproximateMark => IsApproximate ? "≈ " : string.Empty;

        public string IncomeText => MoneyFormatter.Format(Income, CurrencyCode);

        public string ExpenseText => MoneyFormatter.Format(Expense, CurrencyCode);

        public string NetText => MoneyFormatter.FormatSigned(Net, CurrencyCode);

        /// <summary>
        /// One row per currency with any income or expense, the base currency first and always present (Home's cards
        /// show it; the others are listed under them). Currencies are never added together.
        /// </summary>
        public static List<PeriodTotals> Build(IEnumerable<CurrencyAmount> incomes, IEnumerable<CurrencyAmount> expenses,
            string baseCurrencyCode)
        {
            baseCurrencyCode = baseCurrencyCode.ToUpperInvariant();
            var income = CurrencyAmount.Totals(incomes, baseCurrencyCode);
            var expense = CurrencyAmount.Totals(expenses, baseCurrencyCode);
            return income.Concat(expense)
                .Select(a => a.CurrencyCode)
                .Append(baseCurrencyCode)
                .Distinct()
                .OrderBy(code => code == baseCurrencyCode ? 0 : 1)
                .ThenBy(code => code, StringComparer.Ordinal)
                .Select(code => new PeriodTotals(code, CurrencyAmount.In(income, code), CurrencyAmount.In(expense, code)))
                .ToList();
        }
    }
}
