using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;

namespace MoneyRecord.Services.Interfaces
{
    /// <summary>
    /// Transactions in the base currency (<see cref="FromOtherCurrencies"/> of them converted), and those without a rate
    /// (in their own currency).
    /// </summary>
    public sealed record ConversionResult(List<Transaction> Converted, List<Transaction> NotConverted, int FromOtherCurrencies)
    {
        /// <summary>The amounts without a rate, per currency (unsigned), for a "Not converted: …" note.</summary>
        public List<CurrencyAmount> NotConvertedTotals(string baseCurrencyCode) =>
            CurrencyAmount.Totals(NotConverted.Select(t => new CurrencyAmount(t.CurrencyCode, t.Amount)), baseCurrencyCode);
    }

    /// <summary>
    /// What a report shows: its transactions in <see cref="CurrencyCode"/> (unsigned), the amounts that couldn't be
    /// converted (per currency), and whether any were converted (the headline gets a "≈").
    /// </summary>
    public sealed record ReportTransactions(List<Transaction> Transactions, string CurrencyCode,
        List<CurrencyAmount> NotConverted, bool IsApproximate)
    {
        /// <summary>An amount of the report in its currency; "≈ $1,250.00" when something was converted.</summary>
        public string FormatAmount(decimal amount, bool signed = false)
        {
            var text = signed ? MoneyFormatter.FormatSigned(amount, CurrencyCode) : MoneyFormatter.Format(amount, CurrencyCode);
            return IsApproximate ? MoneyFormatter.ApproximateMark + text : text;
        }

        /// <summary>"No exchange rate yet: ¥1,000" for what couldn't be converted; empty when everything was.</summary>
        public string NotConvertedText => NotConverted.Count == 0
            ? string.Empty
            : string.Format(AppResources.NotConvertedNote, CurrencyAmount.Format(NotConverted, separator: " + "));
    }

    /// <summary>The "Show in" switch: offered (<see cref="CanConvert"/>) and on (<see cref="IsConverted"/>, implies the other).</summary>
    public sealed record ConversionState(bool CanConvert, bool IsConverted)
    {
        public static readonly ConversionState Off = new(false, false);
    }

    /// <summary>Home's period totals: the base currency's (converted, when shown so) and every other currency's.</summary>
    public sealed record PeriodTotalsResult(PeriodTotals Base, List<PeriodTotals> Others, bool IsConverted);

    /// <summary>
    /// The "Show in" switch applied: whether totals are converted into the base currency right now, and the totals for
    /// Home, the reports and the budgets either way. Converting needs the download on and some rates stored.
    /// </summary>
    public interface IConversionService
    {
        /// <summary>
        /// Whether totals can be converted (the accounts use several currencies, the download is on and some rates are
        /// stored: the one rule) and are (the user chose it, or hasn't chosen yet).
        /// </summary>
        Task<ConversionState> GetStateAsync();

        /// <summary>Remembers the "Show in" choice (Home's switch, the reports' "≈" chip).</summary>
        void SetConverted(bool converted);

        /// <summary>
        /// Each transaction in the base currency (converted at its date's rate, or its own "Amount in …" first) when it can
        /// be; the rest in its account's currency. Amounts are unsigned.
        /// </summary>
        Task<ConversionResult> ConvertAsync(IEnumerable<Transaction> transactions, IEnumerable<Account> accounts);

        /// <summary>
        /// A report's transactions: all of them converted into the base currency when <paramref name="converted"/>, else
        /// those in <paramref name="currencyCode"/> (<see cref="Helpers.ReportCurrencies.Only"/>).
        /// </summary>
        Task<ReportTransactions> ForReportAsync(IEnumerable<Transaction> transactions, IReadOnlyCollection<Account> accounts,
            bool converted, string currencyCode);

        /// <summary>Home's income / expense / net for the range, converted or by currency per the switch.</summary>
        Task<PeriodTotalsResult> GetPeriodTotalsAsync(DateTime startDate, DateTime endDate);

        /// <summary>
        /// Expenses per category ID for the budgets: converted ones count when totals are converted, else only the base
        /// currency's; the rest is listed per currency.
        /// </summary>
        Task<Dictionary<int, BudgetSpent>> GetExpensesByCategoryAsync(DateTime startDate, DateTime endDate);
    }
}
