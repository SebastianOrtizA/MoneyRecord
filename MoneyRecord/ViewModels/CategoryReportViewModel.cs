using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services.Interfaces;
using System.Collections.ObjectModel;

namespace MoneyRecord.ViewModels
{
    /// <summary>The Expense or Income report: category shares as a heatmap and a list. Set <see cref="Type"/> first.</summary>
    public partial class CategoryReportViewModel : ObservableObject
    {
        private readonly ITransactionEnrichmentService _enrichmentService;
        private readonly IErrorHandler _errorHandler;
        private readonly IDialogService _dialogService;

        // The report's transactions, for the category detail popup
        private List<Transaction> _transactions = [];

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsIncome), nameof(Texts))]
        private CategoryType type = CategoryType.Expense;

        [ObservableProperty]
        private PeriodItem selectedPeriod;

        [ObservableProperty]
        private DateTime customStartDate = DateTime.Now.AddMonths(-1);

        [ObservableProperty]
        private DateTime customEndDate = DateTime.Now;

        [ObservableProperty]
        private bool isCustomPeriodSelected;

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private decimal total;

        [ObservableProperty]
        private bool hasData;

        [ObservableProperty]
        private ObservableCollection<CategoryAmount> categories = [];

        public bool IsIncome => Type == CategoryType.Income;

        public CategoryReportTexts Texts => CategoryReportTexts.For(Type);

        public List<PeriodItem> Periods { get; } = PeriodHelper.GetReportPeriods();

        public CategoryReportViewModel(ITransactionEnrichmentService enrichmentService, IErrorHandler errorHandler, IDialogService dialogService)
        {
            _enrichmentService = enrichmentService;
            _errorHandler = errorHandler;
            _dialogService = dialogService;
            selectedPeriod = PeriodHelper.GetDefaultPeriod();
        }

        public Task InitializeAsync() => LoadReportDataCommand.ExecuteAsync(null);

        /// <summary>Each run cancels the one in progress, so an older period's results never overwrite a newer one's.</summary>
        [RelayCommand(AllowConcurrentExecutions = true)]
        private async Task LoadReportDataAsync(CancellationToken cancellationToken)
        {
            try
            {
                IsLoading = true;

                var (startDate, endDate) = DateRangeHelper.GetDateRange(SelectedPeriod?.Type, CustomStartDate, CustomEndDate);
                // All types: the builder keeps the report's type, and the popup filters by category ID
                var transactions = await _enrichmentService.GetEnrichedTransactionsAsync(startDate, endDate);
                cancellationToken.ThrowIfCancellationRequested();

                _transactions = transactions;
                var report = CategoryReportBuilder.Build(_transactions, Type);
                Total = report.Total;
                HasData = report.Total > 0;
                await MainThread.InvokeOnMainThreadAsync(() => Categories = new(report.Categories));
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

        [RelayCommand]
        private async Task ShowCategoryDetailsAsync(CategoryAmount category)
        {
            if (category == null) return;

            try
            {
                var categoryTransactions = _transactions
                    .Where(t => t.CategoryId == category.CategoryId)
                    .OrderByDescending(t => t.Date)
                    .ToList();

                if (categoryTransactions.Count == 0)
                {
                    await _dialogService.AlertAsync(category.CategoryName, Texts.NoDetailsFound);
                    return;
                }

                var details = categoryTransactions.Select(t =>
                    $"📅 {DateFormatter.Format(t.Date, DateStyle.Medium)}\n" +
                    $"   💰 {MoneyFormatter.Format(Math.Abs(t.Amount), t.CurrencyCode)}\n" +
                    $"   📝 {(string.IsNullOrEmpty(t.Description) ? AppResources.NoDescription : t.Description)}");

                // One subtotal per currency: never "¥10,000 + $20.00 = $10,020.00"
                var totalText = MoneyFormatter.FormatTotals(categoryTransactions.Select(t => (Math.Abs(t.Amount), (string?)t.CurrencyCode)));
                var message = $"{AppResources.Total}: {totalText} ({Plurals.Transactions(categoryTransactions.Count)})\n\n" +
                              string.Join("\n\n", details);

                await _dialogService.AlertAsync($"{Texts.DetailsEmoji} {category.CategoryName}", message, AppResources.Close);
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToLoadCategoryDetails);
            }
        }

        partial void OnSelectedPeriodChanged(PeriodItem value)
        {
            IsCustomPeriodSelected = value?.Type == PeriodType.CustomPeriod;
            LoadReportDataCommand.Execute(null);
        }

        partial void OnCustomStartDateChanged(DateTime value)
        {
            if (IsCustomPeriodSelected)
                LoadReportDataCommand.Execute(null);
        }

        partial void OnCustomEndDateChanged(DateTime value)
        {
            if (IsCustomPeriodSelected)
                LoadReportDataCommand.Execute(null);
        }
    }

    /// <summary>The words that differ between the Expense and Income reports.</summary>
    public sealed record CategoryReportTexts(
        string Title,
        string Analysis,
        string AnalysisHint,
        string TotalLabel,
        string EmptyIcon,
        string NoData,
        string NoDataHint,
        string Heatmap,
        string HeatmapHint,
        string NoDetailsFound,
        string DetailsEmoji)
    {
        public static CategoryReportTexts For(CategoryType type) => type == CategoryType.Income
            ? new(AppResources.IncomeReport, AppResources.IncomeAnalysis, AppResources.IdentifyIncomeCategories,
                AppResources.TotalIncome, "\U000F0114", AppResources.NoIncomeDataFound, AppResources.AddIncomeTransactionsToSee,
                AppResources.IncomeHeatmap, AppResources.AllIncomeCategoriesByPercentage, AppResources.NoIncomeDetailsFound, "💵")
            : new(AppResources.ExpenseReport, AppResources.ExpenseAnalysis, AppResources.IdentifyExpenseCategories,
                AppResources.TotalExpenses, "\U000F0127", AppResources.NoExpenseDataFound, AppResources.AddExpenseTransactionsToSee,
                AppResources.ExpenseHeatmap, AppResources.AllExpenseCategoriesByPercentage, AppResources.NoExpenseDetailsFound, "📊");
    }
}
