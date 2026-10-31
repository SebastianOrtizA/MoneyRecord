using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore;
using MoneyRecord.Charts;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.ViewModels
{
    /// <summary>
    /// The Cash flow report: income and expense per month as two lines; headline = the window's net (income minus
    /// expense) against the window before. A point opens that month's income or expenses.
    /// </summary>
    public partial class CashFlowReportViewModel : MonthlyReportViewModel
    {
        private readonly ITransactionRepository _transactionRepository;

        [ObservableProperty]
        private ISeries[] cashFlowSeries = [];

        public CashFlowReportViewModel(PeriodViewModel period, ITransactionRepository transactionRepository,
            IAccountRepository accountRepository, IConversionService conversion, IErrorHandler errorHandler,
            INavigationService navigationService, TransactionsDrillDown drillDown, ReportExportViewModel export)
            : base(period, accountRepository, conversion, errorHandler, navigationService, drillDown, export)
        {
            _transactionRepository = transactionRepository;
        }

        protected override async Task BuildAsync(DateTime firstMonth, int months, CancellationToken cancellationToken)
        {
            var previousFirst = firstMonth.AddMonths(-months);
            var transactionsTask = _transactionRepository.GetByDateRangeAsync(previousFirst, firstMonth.AddMonths(months).AddTicks(-1));
            var accountsTask = LoadAccountsAsync();
            await Task.WhenAll(transactionsTask, accountsTask);
            cancellationToken.ThrowIfCancellationRequested();

            var shown = await ShownAsync(await transactionsTask, await accountsTask);
            var (code, transactions) = (shown.CurrencyCode, shown.Transactions);

            var totals = MonthlyTotalsBuilder.Build(transactions, firstMonth, months);
            var previous = MonthlyTotalsBuilder.Build(transactions, previousFirst, months);

            HasData = totals.Any(t => t.Income > 0 || t.Expense > 0);
            var net = totals.Sum(t => t.Net);
            HeadlineText = shown.FormatAmount(net, signed: true);
            var delta = new PeriodDelta(net, previous.Sum(t => t.Net));
            DeltaText = delta.Text;
            DeltaTone = delta.ToneFor(higherIsBetter: true);

            MonthAxis = ChartFactory.LabelAxis(totals.Select(t => t.Label));
            MoneyAxis = ChartFactory.MoneyAxis(code);
            // Series 0 = income, 1 = expense: a point opens that type for its month
            CashFlowSeries = ChartFactory.Lines(
                (month, series) => OpenMonth(month, series == 0 ? TransactionType.Income : TransactionType.Expense), code,
                new ChartFactory.Line(AppResources.Incomes, totals.Select(t => t.Income), ChartPalette.Income),
                new ChartFactory.Line(AppResources.Expenses, totals.Select(t => t.Expense), ChartPalette.Expense));
        }
    }
}
