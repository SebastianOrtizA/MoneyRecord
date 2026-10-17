using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.Services.Pdf;

namespace MoneyRecord.Services
{
    public sealed class ReportExportService(
        IConversionService conversion,
        ITransactionEnrichmentService enrichment,
        ICategoryRepository categories,
        IAccountRepository accounts,
        IBudgetSummaryService budgets,
        IBalanceService balances) : IReportExportService
    {
        public async Task<PeriodReport> BuildAsync(ReportRequest request)
        {
            var (start, end) = (request.Start, request.End);
            var (previousStart, previousEnd) = DateRangeHelper.GetPreviousRange(start, end);

            var totals = await conversion.GetPeriodTotalsAsync(start, end);
            var previousTotals = await conversion.GetPeriodTotalsAsync(previousStart, previousEnd);
            var transactions = await enrichment.GetEnrichedTransactionsAsync(start, end);
            var transfers = await enrichment.GetEnrichedTransfersAsync(start, end);
            var allAccounts = await accounts.GetAllAsync();

            return new PeriodReport(
                request,
                DateTime.Now,
                totals,
                previousTotals,
                await CategoriesAsync(transactions, CategoryType.Expense, allAccounts, request),
                await CategoriesAsync(transactions, CategoryType.Income, allAccounts, request),
                await budgets.GetSummaryAsync(request.Period, start, end),
                await BalancesAtAsync(end),
                Entries(transactions, transfers));
        }

        public Task WritePdfAsync(PeriodReport report, string path) => Task.Run(() =>
        {
            using var file = File.Create(path);
            PeriodReportPdf.Write(report, file);
        });

        // The same shares as the Spending / Income report for this currency choice
        private async Task<ReportCategories> CategoriesAsync(List<Transaction> transactions, CategoryType type,
            IReadOnlyCollection<Account> allAccounts, ReportRequest request)
        {
            var transactionType = type == CategoryType.Income ? TransactionType.Income : TransactionType.Expense;
            var shown = await conversion.ForReportAsync(transactions.Where(t => t.Type == transactionType), allAccounts,
                request.Converted, request.CurrencyCode);
            var categoryIds = (await categories.GetByTypeAsync(type)).Select(c => c.Id);
            var report = CategoryReportBuilder.Build(shown.Transactions, type, categoryIds, shown.CurrencyCode);
            return new ReportCategories(report, shown.CurrencyCode, shown.IsApproximate, shown.NotConvertedText);
        }

        /// <summary>Each own account's balance at the end of <paramref name="end"/>: today's, less what changed after it.</summary>
        private async Task<List<AccountBalanceInfo>> BalancesAtAsync(DateTime end)
        {
            var changes = await balances.GetChangesSinceAsync(end.Date.AddDays(1));
            return (await balances.GetAllAccountBalancesAsync())
                .Where(b => !b.IsDebt)
                .Select(b =>
                {
                    b.CurrentBalance -= changes.GetValueOrDefault(b.AccountId);
                    return b;
                })
                .OrderBy(b => b.AccountName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        private static List<ReportEntry> Entries(IEnumerable<Transaction> transactions, IEnumerable<Transfer> transfers) =>
            transactions
                .Select(t => new ReportEntry(t.Date, EntryDescription.Resolve(t.Description, t.CategoryName), t.CategoryName,
                    t.AccountName, t.PayeeName, t.Type == TransactionType.Income ? Math.Abs(t.Amount) : -Math.Abs(t.Amount),
                    t.CurrencyCode, IsTransfer: false))
                .Concat(transfers.Select(t => new ReportEntry(t.Date,
                    string.IsNullOrWhiteSpace(t.Description) ? AppResources.ReportTransfer : t.Description, AppResources.ReportTransfer,
                    $"{t.SourceAccountName} → {t.DestinationAccountName}", string.Empty, Math.Abs(t.Amount), t.CurrencyCode,
                    IsTransfer: true)))
                .OrderBy(e => e.Date)
                .ToList();
    }
}
