using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using MoneyRecord.Charts;
using MoneyRecord.Helpers;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.ViewModels
{
    /// <summary>The Trends report: monthly spending, and income against expenses, over the last few months.</summary>
    public partial class TrendsReportViewModel : ObservableObject
    {
        private readonly ITransactionRepository _transactionRepository;
        private readonly IErrorHandler _errorHandler;

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private bool hasData;

        [ObservableProperty]
        private int selectedMonthCount = 6;

        [ObservableProperty]
        private ISeries[] spendingSeries = [];

        [ObservableProperty]
        private ISeries[] incomeVsExpenseSeries = [];

        [ObservableProperty]
        private Axis[] monthAxis = [];

        [ObservableProperty]
        private Axis[] moneyAxis = [];

        public List<int> MonthCountOptions { get; } = [3, 6, 12];

        public TrendsReportViewModel(ITransactionRepository transactionRepository, IErrorHandler errorHandler)
        {
            _transactionRepository = transactionRepository;
            _errorHandler = errorHandler;
        }

        public Task InitializeAsync() => LoadChartsCommand.ExecuteAsync(null);

        partial void OnSelectedMonthCountChanged(int value) => LoadChartsCommand.Execute(null);

        /// <summary>Each run cancels the one in progress, so an older month count's charts never overwrite a newer one's.</summary>
        [RelayCommand(AllowConcurrentExecutions = true)]
        private async Task LoadChartsAsync(CancellationToken cancellationToken)
        {
            try
            {
                IsLoading = true;

                var months = SelectedMonthCount;
                var (start, end) = MonthlyTotalsBuilder.Window(DateTime.Now, months);
                var transactions = await _transactionRepository.GetByDateRangeAsync(start, end);
                cancellationToken.ThrowIfCancellationRequested();

                var totals = MonthlyTotalsBuilder.Build(transactions, start, months);
                HasData = totals.Any(t => t.Income > 0 || t.Expense > 0);

                // Built on every load, so the colours follow the current light/dark theme
                MonthAxis = ChartFactory.LabelAxis(totals.Select(t => t.Label));
                MoneyAxis = ChartFactory.MoneyAxis();
                SpendingSeries = ChartFactory.Columns(
                    (AppResources.Expenses, totals.Select(t => t.Expense), ChartPalette.Expense));
                IncomeVsExpenseSeries = ChartFactory.Columns(
                    (AppResources.Incomes, totals.Select(t => t.Income), ChartPalette.Income),
                    (AppResources.Expenses, totals.Select(t => t.Expense), ChartPalette.Expense));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // A newer load replaced this one
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToLoadReportData);
            }
            finally
            {
                // The newer load owns the spinner
                if (!cancellationToken.IsCancellationRequested)
                    IsLoading = false;
            }
        }
    }
}
