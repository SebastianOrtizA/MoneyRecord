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
        private readonly IAccountRepository _accountRepository;
        private readonly IConversionService _conversion;
        private readonly IErrorHandler _errorHandler;
        private readonly INavigationService _navigationService;
        private readonly TransactionsDrillDown _drillDown;

        // While the page is shown: a period or report currency change (here or on another tab) reloads it
        private IDisposable? _periodWatch;
        private IDisposable? _currencyWatch;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsIncome), nameof(Texts))]
        private CategoryType type = CategoryType.Expense;

        [ObservableProperty]
        private bool isLoading;

        /// <summary>The total in the report's currency (<see cref="PeriodViewModel.ReportCurrency"/>).</summary>
        [ObservableProperty]
        private string totalText = string.Empty;

        /// <summary>"No exchange rate yet: ¥1,000" while converted, for what had no rate; empty otherwise.</summary>
        [ObservableProperty]
        private string notConvertedText = string.Empty;

        /// <summary>The total against the previous period ("▲ 12% vs previous period").</summary>
        [ObservableProperty]
        private string deltaText = string.Empty;

        [ObservableProperty]
        private DeltaTone deltaTone;

        /// <summary>The slice held under the finger (null when none): shown in the donut centre, not in a tooltip under the finger.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasHeldSlice))]
        private string heldSliceName = string.Empty;

        [ObservableProperty]
        private string heldSliceValueText = string.Empty;

        public bool HasHeldSlice => HeldSliceName.Length > 0;

        private ChartSlice[] _slices = [];
        private decimal _sliceTotal;
        private string _sliceCurrency = string.Empty;

        [RelayCommand]
        private void HoldSlice(ISeries? series)
        {
            var index = series == null ? -1 : Array.IndexOf(Slices, series);
            if (index < 0 || index >= _slices.Length)
            {
                HeldSliceName = string.Empty;
                HeldSliceValueText = string.Empty;
                return;
            }

            HeldSliceName = _slices[index].Name;
            HeldSliceValueText = ChartFactory.SliceText(_slices[index], _sliceTotal, _sliceCurrency);
        }

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
            ITransactionRepository transactionRepository, ICategoryRepository categoryRepository, IAccountRepository accountRepository,
            IConversionService conversion, IErrorHandler errorHandler, INavigationService navigationService,
            TransactionsDrillDown drillDown, ReportExportViewModel export)
        {
            _enrichmentService = enrichmentService;
            _transactionRepository = transactionRepository;
            _categoryRepository = categoryRepository;
            _accountRepository = accountRepository;
            _conversion = conversion;
            _errorHandler = errorHandler;
            _navigationService = navigationService;
            _drillDown = drillDown;
            Period = period;
            Export = export;
        }

        /// <summary>The Export button (the shared period).</summary>
        public ReportExportViewModel Export { get; }

        private TransactionType TransactionType => IsIncome ? TransactionType.Income : TransactionType.Expense;

        /// <summary>The page appeared: load, and follow the shared period until <see cref="Deactivate"/>.</summary>
        public Task InitializeAsync()
        {
            _periodWatch ??= Period.WatchChanges(() => LoadReportDataCommand.Execute(null));
            _currencyWatch ??= Period.WatchCurrency(() => LoadReportDataCommand.Execute(null));
            return LoadReportDataCommand.ExecuteAsync(null);
        }

        /// <summary>The page disappeared: stop following the period (it reloads when it appears again).</summary>
        public void Deactivate()
        {
            _periodWatch?.Dispose();
            _periodWatch = null;
            _currencyWatch?.Dispose();
            _currencyWatch = null;
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
                var baseCode = MoneyFormatter.BaseCurrency;
                var accountsTask = _accountRepository.GetAllAsync();
                var stateTask = _conversion.GetStateAsync();
                await Task.WhenAll(transactionsTask, categoriesTask, accountsTask, stateTask);
                cancellationToken.ThrowIfCancellationRequested();

                // One currency at a time (the chips list the currencies the accounts use), or all converted ("≈")
                var accounts = await accountsTask;
                var currencies = ReportCurrencies.InUse(accounts, baseCode);
                var state = await stateTask;
                await MainThread.InvokeOnMainThreadAsync(() => Period.SetCurrencies(currencies, state));
                var shown = await _conversion.ForReportAsync((await transactionsTask).Where(t => t.Type == TransactionType),
                    accounts, Period.IsConverted, Period.ReportCurrency);
                var code = shown.CurrencyCode;
                var previous = Period.IsConverted
                    ? await ConvertedTotalAsync(previousStart, previousEnd, accounts)
                    : CurrencyAmount.In(
                        await _transactionRepository.SumByCurrencyAsync(TransactionType, previousStart, previousEnd, baseCode), code);
                cancellationToken.ThrowIfCancellationRequested();

                var report = CategoryReportBuilder.Build(shown.Transactions, Type, (await categoriesTask).Select(c => c.Id), code);
                TotalText = shown.FormatAmount(report.Total);
                NotConvertedText = shown.NotConvertedText;
                var delta = new PeriodDelta(report.Total, previous);
                DeltaText = delta.Text;
                DeltaTone = delta.ToneFor(higherIsBetter: IsIncome);
                HasData = report.Total > 0;
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    Categories = new(report.Categories);
                    _slices = [.. report.Slices];
                    _sliceTotal = report.Total;
                    _sliceCurrency = code;
                    HeldSliceName = string.Empty;
                    Slices = ChartFactory.Donut(report.Slices, report.Total, code,
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

        /// <summary>The previous period's total of this type, converted into the base currency.</summary>
        private async Task<decimal> ConvertedTotalAsync(DateTime start, DateTime end, IReadOnlyCollection<Account> accounts)
        {
            var previous = (await _transactionRepository.GetByDateRangeAsync(start, end)).Where(t => t.Type == TransactionType);
            return (await _conversion.ForReportAsync(previous, accounts, converted: true, MoneyFormatter.BaseCurrency))
                .Transactions.Sum(t => t.Amount);
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
            return _navigationService.NavigateToKeepingBackAsync($"//{nameof(TransactionsPage)}");
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
            ? new(AppResources.IncomeReport, AppResources.TotalIncome, "\U000F0114", AppResources.NoIncomeDataFound,
                AppResources.AddIncomeTransactionsToSee)
            : new(AppResources.Spending, AppResources.TotalExpenses, "\U000F0127", AppResources.NoExpenseDataFound,
                AppResources.AddExpenseTransactionsToSee);
    }
}
