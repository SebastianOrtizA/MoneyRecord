using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services.Interfaces;
using System.Collections.ObjectModel;

namespace MoneyRecord.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly IBalanceService _balanceService;
        private readonly ILedgerService _ledgerService;
        private readonly IBudgetSummaryService _budgetSummaryService;
        private readonly IErrorHandler _errorHandler;
        private readonly IDialogService _dialogService;

        /// <summary>Home balance card: the first currency's total (base currency first), with its code when there are several.</summary>
        [ObservableProperty]
        private string balanceText = string.Empty;

        /// <summary>Totals in the other currencies, one line each ("€300.00 EUR"); never added to the first.</summary>
        [ObservableProperty]
        private List<string> otherCurrencyBalances = [];

        [ObservableProperty]
        private decimal totalIncomes;

        [ObservableProperty]
        private decimal totalExpenses;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsNetNegative))]
        private decimal netIncome;

        public bool IsNetNegative => NetIncome < 0;

        [ObservableProperty]
        private PeriodItem selectedPeriod;

        [ObservableProperty]
        private DateTime currentDate = DateTime.Now;

        [ObservableProperty]
        private DateTime customStartDate = DateTime.Now.AddMonths(-1);

        [ObservableProperty]
        private DateTime customEndDate = DateTime.Now;

        [ObservableProperty]
        private bool isCustomPeriodSelected;

        [ObservableProperty]
        private bool isRefreshing;

        [ObservableProperty]
        private bool hasRecentTransactions;

        [ObservableProperty]
        private ObservableCollection<LedgerEntry> recentTransactions = [];

        [ObservableProperty]
        private ObservableCollection<BudgetProgress> budgetSummaries = [];

        [ObservableProperty]
        private bool hasBudgets;

        [ObservableProperty]
        private int budgetsOnTrack;

        [ObservableProperty]
        private int budgetsOverBudget;

        public List<PeriodItem> Periods { get; } = PeriodHelper.GetPeriods();

        public MainViewModel(IBalanceService balanceService, ILedgerService ledgerService, IBudgetSummaryService budgetSummaryService,
            IErrorHandler errorHandler, IDialogService dialogService)
        {
            _balanceService = balanceService;
            _ledgerService = ledgerService;
            _budgetSummaryService = budgetSummaryService;
            _errorHandler = errorHandler;
            _dialogService = dialogService;
            selectedPeriod = PeriodHelper.GetDefaultPeriod();
        }

        public Task InitializeAsync() => LoadDataCommand.ExecuteAsync(null);

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

                var (startDate, endDate) = DateRangeHelper.GetDateRange(SelectedPeriod?.Type, CustomStartDate, CustomEndDate);
                var balancesTask = _balanceService.GetAllAccountBalancesAsync();
                var incomesTask = _balanceService.GetTotalIncomesAsync(startDate, endDate);
                var expensesTask = _balanceService.GetTotalExpensesAsync(startDate, endDate);
                var entriesTask = _ledgerService.GetEntriesAsync(startDate, endDate);
                var budgetsTask = _budgetSummaryService.GetSummaryAsync(
                    SelectedPeriod?.Type ?? PeriodType.CalendarMonth, CustomStartDate, CustomEndDate);
                await Task.WhenAll(balancesTask, incomesTask, expensesTask, entriesTask, budgetsTask);
                cancellationToken.ThrowIfCancellationRequested();

                var balanceLines = CurrencyBalance.DisplayLines(
                    CurrencyBalance.FromAccounts(await balancesTask, MoneyFormatter.ResolveCurrencyCode(null)));
                BalanceText = balanceLines[0];
                OtherCurrencyBalances = balanceLines.Skip(1).ToList();

                TotalIncomes = await incomesTask;
                TotalExpenses = await expensesTask;
                NetIncome = TotalIncomes - TotalExpenses;
                // Re-format even when the values didn't change: the base currency may have (Settings)
                OnPropertyChanged(nameof(TotalIncomes));
                OnPropertyChanged(nameof(TotalExpenses));
                OnPropertyChanged(nameof(NetIncome));

                // Whole transfers: the Recent card lists a transfer once
                var entries = await entriesTask;
                RecentTransactions = new(entries.OrderByDescending(e => e.Date).Take(5));
                HasRecentTransactions = RecentTransactions.Count > 0;

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

        [RelayCommand]
        private async Task ShowAccountBalancesAsync()
        {
            try
            {
                var balances = await _balanceService.GetAllAccountBalancesAsync();

                var message = string.Join("\n\n", balances.Select(b =>
                    $"🏦 {b.AccountName}\n" +
                    $"   {AppResources.Balance}: {MoneyFormatter.Format(b.CurrentBalance, b.CurrencyCode)}\n" +
                    $"   {AppResources.LastActivity}: {b.LastActivityDateDisplay}"));

                if (string.IsNullOrEmpty(message))
                    message = AppResources.NoAccountsFound;

                await _dialogService.AlertAsync(AppResources.AccountBalances, message);
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToLoadAccountBalances);
            }
        }

        partial void OnSelectedPeriodChanged(PeriodItem value)
        {
            IsCustomPeriodSelected = value?.Type == PeriodType.CustomPeriod;
            LoadDataCommand.Execute(null);
        }

        partial void OnCustomStartDateChanged(DateTime value)
        {
            if (IsCustomPeriodSelected)
                LoadDataCommand.Execute(null);
        }

        partial void OnCustomEndDateChanged(DateTime value)
        {
            if (IsCustomPeriodSelected)
                LoadDataCommand.Execute(null);
        }
    }
}
