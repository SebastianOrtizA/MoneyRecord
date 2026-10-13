using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using MoneyRecord.Charts;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.Views;
using System.Collections.ObjectModel;

namespace MoneyRecord.ViewModels
{
    /// <summary>
    /// The Spending or Income report for the shared period: category shares as a donut and a ranked list, the total
    /// against the previous period. A slice or a row opens the Transactions tab filtered to it. Set <see cref="Type"/> first.
    /// </summary>
    public partial class CategoryReportViewModel : ObservableObject
    {
        private readonly ITransactionEnrichmentService _enrichmentService;
        private readonly ITransactionRepository _transactionRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly IErrorHandler _errorHandler;
        private readonly INavigationService _navigationService;
        private readonly TransactionsDrillDown _drillDown;

        // While the page is shown: a period change (here or on another tab) reloads it
        private IDisposable? _periodWatch;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsIncome), nameof(Texts))]
        private CategoryType type = CategoryType.Expense;

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private decimal total;

        /// <summary>The total against the previous period ("▲ 12% vs previous period").</summary>
        [ObservableProperty]
        private string deltaText = string.Empty;

        [ObservableProperty]
        private bool hasData;

        [ObservableProperty]
        private ObservableCollection<CategoryAmount> categories = [];

        /// <summary>The donut: one slice per category with its own colour, then "Other".</summary>
        [ObservableProperty]
        private ISeries[] slices = [];

        public bool IsIncome => Type == CategoryType.Income;

        public CategoryReportTexts Texts => CategoryReportTexts.For(Type);

        /// <summary>The shared period (Home, Transactions, Budgets, Spending / Income).</summary>
        public PeriodViewModel Period { get; }

        public CategoryReportViewModel(PeriodViewModel period, ITransactionEnrichmentService enrichmentService,
            ITransactionRepository transactionRepository, ICategoryRepository categoryRepository, IErrorHandler errorHandler,
            INavigationService navigationService, TransactionsDrillDown drillDown)
        {
            _enrichmentService = enrichmentService;
            _transactionRepository = transactionRepository;
            _categoryRepository = categoryRepository;
            _errorHandler = errorHandler;
            _navigationService = navigationService;
            _drillDown = drillDown;
            Period = period;
        }

        private TransactionType TransactionType => IsIncome ? TransactionType.Income : TransactionType.Expense;

        /// <summary>The page appeared: load, and follow the shared period until <see cref="Deactivate"/>.</summary>
        public Task InitializeAsync()
        {
            _periodWatch ??= Period.WatchChanges(() => LoadReportDataCommand.Execute(null));
            return LoadReportDataCommand.ExecuteAsync(null);
        }

        /// <summary>The page disappeared: stop following the period (it reloads when it appears again).</summary>
        public void Deactivate()
        {
            _periodWatch?.Dispose();
            _periodWatch = null;
        }

        /// <summary>Each run cancels the one in progress, so an older period's results never overwrite a newer one's.</summary>
        [RelayCommand(AllowConcurrentExecutions = true)]
        private async Task LoadReportDataAsync(CancellationToken cancellationToken)
        {
            try
            {
                IsLoading = true;

                var (startDate, endDate) = Period.Range;
                var (previousStart, previousEnd) = Period.PreviousRange;
                var transactionsTask = _enrichmentService.GetEnrichedTransactionsAsync(startDate, endDate);
                var categoriesTask = _categoryRepository.GetByTypeAsync(Type);
                var previousTask = _transactionRepository.SumCentsAsync(TransactionType, previousStart, previousEnd);
                await Task.WhenAll(transactionsTask, categoriesTask, previousTask);
                cancellationToken.ThrowIfCancellationRequested();

                var report = CategoryReportBuilder.Build(await transactionsTask, Type, (await categoriesTask).Select(c => c.Id));
                Total = report.Total;
                DeltaText = new PeriodDelta(report.Total, Money.FromCents(await previousTask)).Text;
                HasData = report.Total > 0;
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    Categories = new(report.Categories);
                    Slices = ChartFactory.Donut(report.Slices, report.Total,
                        slice => OpenTransactionsCommand.Execute(slice.CategoryId));
                });
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

        /// <summary>A row of the list: its transactions.</summary>
        [RelayCommand]
        private Task OpenCategoryAsync(CategoryAmount? category) =>
            category == null ? Task.CompletedTask : OpenTransactionsAsync(category.CategoryId);

        /// <summary>
        /// The Transactions tab for the same period, filtered to this type and category (all of the type for the
        /// "Other" slice, which has no single category).
        /// </summary>
        [RelayCommand]
        private Task OpenTransactionsAsync(int? categoryId)
        {
            _drillDown.Request(categoryId, TransactionType);
            return _navigationService.NavigateToAsync($"//{nameof(TransactionsPage)}");
        }
    }

    /// <summary>The words that differ between the Spending and Income reports.</summary>
    public sealed record CategoryReportTexts(
        string Title,
        string TotalLabel,
        string EmptyIcon,
        string NoData,
        string NoDataHint)
    {
        public static CategoryReportTexts For(CategoryType type) => type == CategoryType.Income
            ? new(AppResources.Income, AppResources.TotalIncome, "\U000F0114", AppResources.NoIncomeDataFound,
                AppResources.AddIncomeTransactionsToSee)
            : new(AppResources.Spending, AppResources.TotalExpenses, "\U000F0127", AppResources.NoExpenseDataFound,
                AppResources.AddExpenseTransactionsToSee);
    }
}
