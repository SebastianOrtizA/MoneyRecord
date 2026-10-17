using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Models
{
    /// <summary>
    /// What a report export covers: the range, the period type (budgets project their limits onto it) and the reports'
    /// currency choice (<see cref="ViewModels.PeriodViewModel.ReportCurrency"/>, or everything converted).
    /// </summary>
    public sealed record ReportRequest(DateTime Start, DateTime End, PeriodType Period, string CurrencyCode, bool Converted);

    /// <summary>
    /// Everything the PDF report shows for a period: the totals per currency (and the period before, for the change), the
    /// category shares of both types in the reports' currency, the budgets, each account's balance at the end and the
    /// period's entries.
    /// </summary>
    public sealed record PeriodReport(
        ReportRequest Request,
        DateTime CreatedAt,
        PeriodTotalsResult Totals,
        PeriodTotalsResult PreviousTotals,
        ReportCategories Expenses,
        ReportCategories Income,
        List<BudgetProgress> Budgets,
        List<AccountBalanceInfo> Balances,
        List<ReportEntry> Entries)
    {
        /// <summary>The totals rows: the base currency's (converted, when shown so) first, then every other currency's.</summary>
        public IEnumerable<PeriodTotals> TotalsRows => Totals.Others.Prepend(Totals.Base);

        /// <summary>The same currency's totals the period before (zeros when it had none).</summary>
        public PeriodTotals PreviousOf(PeriodTotals totals) =>
            PreviousTotals.Others.Prepend(PreviousTotals.Base).FirstOrDefault(p => p.CurrencyCode == totals.CurrencyCode)
            ?? new PeriodTotals(totals.CurrencyCode, 0, 0);

        /// <summary>The balances' totals per currency (never added across currencies), base currency first.</summary>
        public List<CurrencyAmount> BalanceTotals => CurrencyAmount.Totals(
            Balances.Select(b => new CurrencyAmount(b.CurrencyCode, b.CurrentBalance)), Helpers.MoneyFormatter.BaseCurrency);
    }

    /// <summary>A report's category shares in one currency, "≈" when converted, and what had no rate.</summary>
    public sealed record ReportCategories(CategoryReport Report, string CurrencyCode, bool IsApproximate, string NotConvertedText)
    {
        public string FormatAmount(decimal amount)
        {
            var text = Helpers.MoneyFormatter.Format(amount, CurrencyCode);
            return IsApproximate ? Helpers.MoneyFormatter.ApproximateMark + text : text;
        }
    }

    /// <summary>
    /// One row of the report's entry list: a transaction (signed: income +, expense −) or a transfer ("From → To", the
    /// amount that left in the source currency).
    /// </summary>
    public sealed record ReportEntry(DateTime Date, string Description, string Category, string Account, string Payee,
        decimal Amount, string CurrencyCode, bool IsTransfer);
}
