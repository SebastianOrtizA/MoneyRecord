using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.Views;
using System.Collections.ObjectModel;

namespace MoneyRecord.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly IBalanceService _balanceService;
        private readonly ILedgerService _ledgerService;
        private readonly IBudgetSummaryService _budgetSummaryService;
        private readonly IRecurringTransactionService _recurringService;
        private readonly IErrorHandler _errorHandler;
        private readonly INavigationService _navigationService;
        private readonly ICrashReportConsent _crashReports;
        private readonly IDialogService _dialogService;
        private readonly NotificationLaunch _notificationLaunch;
        private readonly IPrivacySettings _privacy;
        private readonly IDatabaseEncryption _databaseEncryption;
        private bool _askingCrashReports;

        // While the page is shown: a period change (here or on another tab) reloads it
        private IDisposable? _periodWatch;

        /// <summary>Home balance card: the first currency's total (base currency first), with its code when there are several.</summary>
        [ObservableProperty]
        private BalanceLine primaryBalance = new(string.Empty, null);

        /// <summary>Totals in the other currencies, one line each ("€300.00 EUR"); never added to the first.</summary>
        [ObservableProperty]
        private List<BalanceLine> otherBalances = [];

        /// <summary>Privacy mode: the balance card and the period totals show "••••••" (saved).</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowOtherTotals))]
        private bool hideBalances;

        partial void OnHideBalancesChanged(bool value) => _privacy.HideBalances = value;

        [RelayCommand]
        private void ToggleHideBalances() => HideBalances = !HideBalances;

        /// <summary>The period's totals in the base currency (the three cards).</summary>
        [ObservableProperty]
        private PeriodTotals baseTotals = new(string.Empty, 0, 0);

        /// <summary>The period's totals in every other currency, one row each under the cards (never added together).</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowOtherTotals))]
        private List<PeriodTotals> otherTotals = [];

        /// <summary>The other-currency rows show when there are any and balances aren't hidden.</summary>
        public bool ShowOtherTotals => OtherTotals.Count > 0 && !HideBalances;

        [ObservableProperty]
        private DateTime currentDate = DateTime.Now;

        [ObservableProperty]
        private bool isRefreshing;

        [ObservableProperty]
        private bool hasRecentTransactions;

        [ObservableProperty]
        private ObservableCollection<LedgerEntry> recentTransactions = [];

        /// <summary>The next scheduled recurring transactions (the period doesn't apply).</summary>
        [ObservableProperty]
        private List<UpcomingEntry> upcomingEntries = [];

        [ObservableProperty]
        private bool hasUpcoming;

        [ObservableProperty]
        private ObservableCollection<BudgetProgress> budgetSummaries = [];

        [ObservableProperty]
        private bool hasBudgets;

        [ObservableProperty]
        private int budgetsOnTrack;

        [ObservableProperty]
        private int budgetsOverBudget;

        /// <summary>The shared period (Home, Transactions, Budgets, Spending / Income).</summary>
        public PeriodViewModel Period { get; }

        public MainViewModel(PeriodViewModel period, IBalanceService balanceService, ILedgerService ledgerService, IBudgetSummaryService budgetSummaryService,
            IRecurringTransactionService recurringService, IErrorHandler errorHandler,
            INavigationService navigationService, ICrashReportConsent crashReports, IDialogService dialogService,
            NotificationLaunch notificationLaunch, IPrivacySettings privacy, IDatabaseEncryption databaseEncryption)
        {
            _balanceService = balanceService;
            _ledgerService = ledgerService;
            _budgetSummaryService = budgetSummaryService;
            _recurringService = recurringService;
            _errorHandler = errorHandler;
            _navigationService = navigationService;
            _crashReports = crashReports;
            _dialogService = dialogService;
            _notificationLaunch = notificationLaunch;
            _privacy = privacy;
            _databaseEncryption = databaseEncryption;
            HideBalances = privacy.HideBalances;
            Period = period;

            // Home lives as long as the app: after a language change in Settings, a new value makes the bound date
            // text format again (the period pickers refresh themselves)
            LocalizationService.Instance.LanguageChanged += (_, _) => CurrentDate = DateTime.Now;
        }

        /// <summary>The page appeared: load, and follow the shared period until <see cref="Deactivate"/>.</summary>
        public async Task InitializeAsync()
        {
            _periodWatch ??= Period.WatchChanges(() => LoadDataCommand.Execute(null));
            await LoadDataCommand.ExecuteAsync(null);
            // The encrypted database's key was gone at start: say why the data is missing
            if (_databaseEncryption.TakeUnreadableDatabaseNotice())
                await _dialogService.AlertAsync(AppResources.UnreadableDatabaseTitle, AppResources.UnreadableDatabaseMessage);
            // Opened from a notification: its page, now that the Shell is ready
            await _notificationLaunch.OpenPendingAsync();
            await AskCrashReportsOnceAsync();
        }

        /// <summary>Once, a week after first use: may the app send crash reports? (Only builds that can send ask.)</summary>
        private async Task AskCrashReportsOnceAsync()
        {
            if (_askingCrashReports || !_crashReports.ShouldAskNow())
                return;

            _askingCrashReports = true;
            try
            {
                var agreed = await _dialogService.ConfirmAsync(AppResources.CrashReportsQuestionTitle,
                    AppResources.CrashReportsQuestion, AppResources.CrashReportsAccept, AppResources.CrashReportsDecline);
                _crashReports.Enabled = agreed;
            }
            finally
            {
                _askingCrashReports = false;
            }
        }

        /// <summary>The page disappeared: stop following the period (it reloads when it appears again).</summary>
        public void Deactivate()
        {
            _periodWatch?.Dispose();
            _periodWatch = null;
        }

        /// <summary>
        /// Reloads Home. Each run cancels the one in progress (period changes, pull to refresh), so the
        /// results of an older period never overwrite the newer ones.
        /// </summary>
        [RelayCommand(AllowConcurrentExecutions = true)]
        private async Task LoadDataAsync(CancellationToken cancellationToken)
        {
            try
            {
                IsRefreshing = true;

                var (startDate, endDate) = Period.Range;
                var balancesTask = _balanceService.GetAllAccountBalancesAsync();
                var incomesTask = _balanceService.GetTotalIncomesAsync(startDate, endDate);
                var expensesTask = _balanceService.GetTotalExpensesAsync(startDate, endDate);
                var entriesTask = _ledgerService.GetEntriesAsync(startDate, endDate);
                var budgetsTask = _budgetSummaryService.GetSummaryAsync(
                    Period.SelectedPeriod?.Type ?? PeriodType.CalendarMonth, Period.CustomStartDate, Period.CustomEndDate);
                var upcomingTask = _recurringService.GetUpcomingAsync();
                await Task.WhenAll(balancesTask, incomesTask, expensesTask, entriesTask, budgetsTask, upcomingTask);
                cancellationToken.ThrowIfCancellationRequested();

                var balanceLines = CurrencyBalance.DisplayLines(
                    CurrencyBalance.FromAccounts(await balancesTask, MoneyFormatter.ResolveCurrencyCode(null)));
                PrimaryBalance = balanceLines[0];
                OtherBalances = balanceLines.Skip(1).ToList();

                // A new record even when the values didn't change: the base currency may have (Settings)
                var totals = PeriodTotals.Build(await incomesTask, await expensesTask, MoneyFormatter.ResolveCurrencyCode(null));
                BaseTotals = totals[0];
                OtherTotals = totals.Skip(1).ToList();

                // Whole transfers: the Recent card lists a transfer once
                var entries = await entriesTask;
                RecentTransactions = new(entries.OrderByDescending(e => e.Date).Take(5));
                HasRecentTransactions = RecentTransactions.Count > 0;

                UpcomingEntries = (await upcomingTask).Take(3).ToList();
                HasUpcoming = UpcomingEntries.Count > 0;

                // The budget card uses the Home period, like the Budget page does with its own
                var budgets = await budgetsTask;
                BudgetsOnTrack = budgets.Count(b => !b.IsOverBudget);
                BudgetsOverBudget = budgets.Count(b => b.IsOverBudget);
                BudgetSummaries = new(budgets.Take(3));
                HasBudgets = budgets.Count > 0;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // A newer load replaced this one
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToLoadTransactions);
            }
            finally
            {
                // The newer load owns the spinner
                if (!cancellationToken.IsCancellationRequested)
                    IsRefreshing = false;
            }
        }

        /// <summary>The balance card opens all accounts; a currency line opens the accounts in that currency.</summary>
        [RelayCommand]
        private Task OpenAccountsAsync(BalanceLine? line) => line?.CurrencyCode is { } code
            ? _navigationService.NavigateToAsync($"{nameof(ManageAccountsPage)}?CurrencyCode={code}")
            : _navigationService.NavigateToAsync(nameof(ManageAccountsPage));

        [RelayCommand]
        private Task SeeAllTransactionsAsync() => _navigationService.NavigateToAsync($"//{nameof(TransactionsPage)}");

        [RelayCommand]
        private Task SeeAllUpcomingAsync() =>
            _navigationService.NavigateToAsync($"{nameof(RecurringTransactionsPage)}?Show={RecurringTransactionsViewModel.ShowUpcoming}");

        [RelayCommand]
        private Task ViewAllBudgetsAsync() => _navigationService.NavigateToAsync($"//{nameof(BudgetPage)}");
    }
}
