using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore.SkiaSharpView;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.Views;

namespace MoneyRecord.ViewModels
{
    /// <summary>
    /// A time-series report (Trends, Cash flow, Net worth): the last <see cref="PeriodViewModel.MonthCount"/> months,
    /// shared between them, with a headline against the months before. Tapping a month opens the Transactions tab
    /// for that month.
    /// </summary>
    public abstract partial class MonthlyReportViewModel : ObservableObject
    {
        private readonly IAccountRepository _accountRepository;
        private readonly IConversionService _conversion;
        private readonly IErrorHandler _errorHandler;
        private readonly INavigationService _navigationService;
        private readonly TransactionsDrillDown _drillDown;

        // While the page is shown: a month count or report currency change (here or on another report) reloads it
        private IDisposable? _monthsWatch;
        private IDisposable? _currencyWatch;

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private bool hasData;

        /// <summary>The report's headline number for the window, in the report's currency.</summary>
        [ObservableProperty]
        private string headlineText = string.Empty;

        /// <summary>"No exchange rate yet: ¥1,000" while converted, for what had no rate; empty otherwise.</summary>
        [ObservableProperty]
        private string notConvertedText = string.Empty;

        /// <summary>The headline against the window before ("▲ 12% vs previous period").</summary>
        [ObservableProperty]
        private string deltaText = string.Empty;

        [ObservableProperty]
        private Axis[] monthAxis = [];

        [ObservableProperty]
        private Axis[] moneyAxis = [];

        protected MonthlyReportViewModel(PeriodViewModel period, IAccountRepository accountRepository, IConversionService conversion,
            IErrorHandler errorHandler, INavigationService navigationService, TransactionsDrillDown drillDown)
        {
            _accountRepository = accountRepository;
            _conversion = conversion;
            _errorHandler = errorHandler;
            _navigationService = navigationService;
            _drillDown = drillDown;
            Period = period;
        }

        /// <summary>The shared period; these reports use its <see cref="PeriodViewModel.MonthCount"/>.</summary>
        public PeriodViewModel Period { get; }

        /// <summary>The first month shown, in the order the report's rows come in.</summary>
        protected DateTime FirstMonth { get; private set; }

        /// <summary>The page appeared: load, and follow the shared month count until <see cref="Deactivate"/>.</summary>
        public Task InitializeAsync()
        {
            _monthsWatch ??= Period.WatchMonthCount(() => LoadCommand.Execute(null));
            _currencyWatch ??= Period.WatchCurrency(() => LoadCommand.Execute(null));
            return LoadCommand.ExecuteAsync(null);
        }

        /// <summary>The page disappeared: stop following the month count (it reloads when it appears again).</summary>
        public void Deactivate()
        {
            _monthsWatch?.Dispose();
            _monthsWatch = null;
            _currencyWatch?.Dispose();
            _currencyWatch = null;
        }

        /// <summary>
        /// Every account, after the shared currency chips are updated from them: the report then shows
        /// <see cref="PeriodViewModel.ReportCurrency"/> only, or everything converted while <see cref="PeriodViewModel.IsConverted"/>
        /// (currencies are never added together unconverted).
        /// </summary>
        protected async Task<List<Account>> LoadAccountsAsync()
        {
            var accounts = await _accountRepository.GetAllAsync();
            var currencies = ReportCurrencies.InUse(accounts, MoneyFormatter.BaseCurrency);
            var state = await _conversion.GetStateAsync();
            await MainThread.InvokeOnMainThreadAsync(() => Period.SetCurrencies(currencies, state));
            return accounts;
        }

        /// <summary>The transactions the report shows (after <see cref="LoadAccountsAsync"/>), and its note.</summary>
        protected async Task<ReportTransactions> ShownAsync(IEnumerable<Transaction> transactions, IReadOnlyCollection<Account> accounts)
        {
            var shown = await _conversion.ForReportAsync(transactions, accounts, Period.IsConverted, Period.ReportCurrency);
            NotConvertedText = shown.NotConvertedText;
            return shown;
        }

        /// <summary>Loads the window [<paramref name="firstMonth"/>, this month]; charts are built here, so they follow the theme.</summary>
        protected abstract Task BuildAsync(DateTime firstMonth, int months, CancellationToken cancellationToken);

        /// <summary>Each run cancels the one in progress, so an older month count's charts never overwrite a newer one's.</summary>
        [RelayCommand(AllowConcurrentExecutions = true)]
        private async Task LoadAsync(CancellationToken cancellationToken)
        {
            try
            {
                IsLoading = true;
                var months = Period.MonthCount;
                var today = DateTime.Today;
                FirstMonth = new DateTime(today.Year, today.Month, 1).AddMonths(-(months - 1));
                await BuildAsync(FirstMonth, months, cancellationToken);
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

        /// <summary>
        /// The Transactions tab for the tapped month (it becomes the shared period), filtered to <paramref name="type"/>
        /// (null = every entry).
        /// </summary>
        protected void OpenMonth(int monthIndex, TransactionType? type)
        {
            Period.ShowMonth(FirstMonth.AddMonths(monthIndex));
            _drillDown.Request(null, type);
            MainThread.BeginInvokeOnMainThread(async () =>
                await _navigationService.NavigateToAsync($"//{nameof(TransactionsPage)}"));
        }
    }
}
