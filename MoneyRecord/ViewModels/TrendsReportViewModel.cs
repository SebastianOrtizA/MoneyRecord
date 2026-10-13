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
    /// The Trends report: spending per month (headline = the window's spending against the window before) and budget vs
    /// actual per month for the budgeted categories. A bar opens that month's expenses.
    /// </summary>
    public partial class TrendsReportViewModel : MonthlyReportViewModel
    {
        private readonly ITransactionRepository _transactionRepository;
        private readonly IBudgetRepository _budgetRepository;

        [ObservableProperty]
        private ISeries[] spendingSeries = [];

        [ObservableProperty]
        private ISeries[] budgetSeries = [];

        [ObservableProperty]
        private bool hasBudgets;

        public TrendsReportViewModel(PeriodViewModel period, ITransactionRepository transactionRepository,
            IBudgetRepository budgetRepository, IErrorHandler errorHandler, INavigationService navigationService,
            TransactionsDrillDown drillDown)
            : base(period, errorHandler, navigationService, drillDown)
        {
            _transactionRepository = transactionRepository;
            _budgetRepository = budgetRepository;
        }

        protected override async Task BuildAsync(DateTime firstMonth, int months, CancellationToken cancellationToken)
        {
            // The window and the one before it, in one query
            var previousFirst = firstMonth.AddMonths(-months);
            var transactionsTask = _transactionRepository.GetByDateRangeAsync(previousFirst, firstMonth.AddMonths(months).AddTicks(-1));
            var budgetsTask = _budgetRepository.GetActiveBudgetsAsync();
            await Task.WhenAll(transactionsTask, budgetsTask);
            cancellationToken.ThrowIfCancellationRequested();

            var transactions = await transactionsTask;
            var budgets = await budgetsTask;
            var totals = MonthlyTotalsBuilder.Build(transactions, firstMonth, months);
            var previous = MonthlyTotalsBuilder.Build(transactions, previousFirst, months);
            var budgetRows = BudgetVsActualBuilder.Build(budgets, transactions, firstMonth, months);

            HasData = totals.Any(t => t.Expense > 0);
            HasBudgets = budgets.Count > 0;
            Headline = totals.Sum(t => t.Expense);
            DeltaText = new PeriodDelta(Headline, previous.Sum(t => t.Expense)).Text;

            MonthAxis = ChartFactory.LabelAxis(totals.Select(t => t.Label));
            MoneyAxis = ChartFactory.MoneyAxis();
            SpendingSeries = ChartFactory.Columns((month, _) => OpenMonth(month, TransactionType.Expense),
                new ChartFactory.Line(AppResources.Expenses, totals.Select(t => t.Expense), ChartPalette.Expense));
            BudgetSeries = ChartFactory.Columns((month, _) => OpenMonth(month, TransactionType.Expense),
                new ChartFactory.Line(AppResources.BudgetSeries, budgetRows.Select(b => b.Limit), ChartPalette.Budget),
                new ChartFactory.Line(AppResources.SpentSeries, budgetRows.Select(b => b.Spent), ChartPalette.Expense));
        }
    }
}
