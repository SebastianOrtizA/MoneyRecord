using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore.SkiaSharpView;
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
        private readonly IErrorHandler _errorHandler;
        private readonly INavigationService _navigationService;
        private readonly TransactionsDrillDown _drillDown;

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private bool hasData;

        /// <summary>The report's headline number for the window.</summary>
        [ObservableProperty]
        private decimal headline;

        /// <summary>The headline against the window before ("▲ 12% vs previous period").</summary>
        [ObservableProperty]
        private string deltaText = string.Empty;

        [ObservableProperty]
        private Axis[] monthAxis = [];

        [ObservableProperty]
        private Axis[] moneyAxis = [];

        protected MonthlyReportViewModel(PeriodViewModel period, IErrorHandler errorHandler,
            INavigationService navigationService, TransactionsDrillDown drillDown)
        {
            _errorHandler = errorHandler;
            _navigationService = navigationService;
            _drillDown = drillDown;
            Period = period;
            Period.MonthCountChanged += () => LoadCommand.Execute(null);
        }

        /// <summary>The shared period; these reports use its <see cref="PeriodViewModel.MonthCount"/>.</summary>
        public PeriodViewModel Period { get; }

        /// <summary>The first month shown, in the order the report's rows come in.</summary>
        protected DateTime FirstMonth { get; private set; }

        public Task InitializeAsync() => LoadCommand.ExecuteAsync(null);

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
