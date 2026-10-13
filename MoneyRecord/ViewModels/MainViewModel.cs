using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
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
        private readonly IErrorHandler _errorHandler;
        private readonly INavigationService _navigationService;

        /// <summary>Home balance card: the first currency's total (base currency first), with its code when there are several.</summary>
        [ObservableProperty]
        private BalanceLine primaryBalance = new(string.Empty, null);

        /// <summary>Totals in the other currencies, one line each ("€300.00 EUR"); never added to the first.</summary>
        [ObservableProperty]
        private List<BalanceLine> otherBalances = [];

        [ObservableProperty]
        private decimal totalIncomes;

        [ObservableProperty]
        private decimal totalExpenses;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsNetNegative))]
        private decimal netIncome;

        public bool IsNetNegative => NetIncome < 0;

        [ObservableProperty]
        private DateTime currentDate = DateTime.Now;

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

        /// <summary>The shared period (Home, Transactions, Budgets, Spending / Income).</summary>
        public PeriodViewModel Period { get; }

        public MainViewModel(PeriodViewModel period, IBalanceService balanceService, ILedgerService ledgerService, IBudgetSummaryService budgetSummaryService,
            IErrorHandler errorHandler, INavigationService navigationService)
        {
            _balanceService = balanceService;
            _ledgerService = ledgerService;
            _budgetSummaryService = budgetSummaryService;
            _errorHandler = errorHandler;
            _navigationService = navigationService;
            Period = period;
            // The shared period changed (here or on another tab): show the new one
            Period.Changed += () => LoadDataCommand.Execute(null);
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

                var (startDate, endDate) = Period.Range;
                var balancesTask = _balanceService.GetAllAccountBalancesAsync();
                var incomesTask = _balanceService.GetTotalIncomesAsync(startDate, endDate);
                var expensesTask = _balanceService.GetTotalExpensesAsync(startDate, endDate);
                var entriesTask = _ledgerService.GetEntriesAsync(startDate, endDate);
                var budgetsTask = _budgetSummaryService.GetSummaryAsync(
                    Period.SelectedPeriod?.Type ?? PeriodType.CalendarMonth, Period.CustomStartDate, Period.CustomEndDate);
                await Task.WhenAll(balancesTask, incomesTask, expensesTask, entriesTask, budgetsTask);
                cancellationToken.ThrowIfCancellationRequested();

                var balanceLines = CurrencyBalance.DisplayLines(
                    CurrencyBalance.FromAccounts(await balancesTask, MoneyFormatter.ResolveCurrencyCode(null)));
                PrimaryBalance = balanceLines[0];
                OtherBalances = balanceLines.Skip(1).ToList();

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

        /// <summary>The balance card opens all accounts; a currency line opens the accounts in that currency.</summary>
        [RelayCommand]
        private Task OpenAccountsAsync(BalanceLine? line) => line?.CurrencyCode is { } code
            ? _navigationService.NavigateToAsync($"{nameof(ManageAccountsPage)}?CurrencyCode={code}")
            : _navigationService.NavigateToAsync(nameof(ManageAccountsPage));

        [RelayCommand]
        private Task SeeAllTransactionsAsync() => _navigationService.NavigateToAsync($"//{nameof(TransactionsPage)}");

        [RelayCommand]
        private Task ViewAllBudgetsAsync() => _navigationService.NavigateToAsync($"//{nameof(BudgetPage)}");
    }
}
