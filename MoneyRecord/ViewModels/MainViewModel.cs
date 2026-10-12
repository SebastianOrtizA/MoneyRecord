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
        private readonly ITransactionRepository _transactionRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly IErrorHandler _errorHandler;
        private readonly IDialogService _dialogService;
        private readonly INavigationService _navigationService;

        private List<LedgerEntry> _entries = [];
        private List<LedgerEntry> _accountEntries = [];
        private List<AccountBalanceInfo> _accountBalances = [];
        private List<Account> _accounts = [];

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
        private GroupingMode currentGroupingMode = GroupingMode.Category;

        [ObservableProperty]
        private bool isAscending;

        [ObservableProperty]
        private bool isRefreshing;

        [ObservableProperty]
        private bool hasTransactions;

        [ObservableProperty]
        private ObservableCollection<LedgerEntry> transactions = [];

        [ObservableProperty]
        private ObservableCollection<TransactionGroup> groupedTransactions = [];

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

        [ObservableProperty]
        private bool hasActiveFilters;

        [ObservableProperty]
        private int filteredCount;

        [ObservableProperty]
        private int totalCount;

        public List<PeriodItem> Periods { get; } = PeriodHelper.GetPeriods();

        public TransactionFilterViewModel Filters { get; } = new();

        public MainViewModel(IBalanceService balanceService, ILedgerService ledgerService, IBudgetSummaryService budgetSummaryService, ITransactionRepository transactionRepository,
            IAccountRepository accountRepository, IErrorHandler errorHandler, IDialogService dialogService, INavigationService navigationService)
        {
            _balanceService = balanceService;
            _ledgerService = ledgerService;
            _budgetSummaryService = budgetSummaryService;
            _transactionRepository = transactionRepository;
            _accountRepository = accountRepository;
            _errorHandler = errorHandler;
            _dialogService = dialogService;
            _navigationService = navigationService;
            selectedPeriod = PeriodHelper.GetDefaultPeriod();
            Filters.Changed += ApplyFilters;
        }

        public Task InitializeAsync() => LoadDataCommand.ExecuteAsync(null);

        /// <summary>
        /// Reloads Home. Each run cancels the one in progress (period changes, pull to refresh, a delete), so the
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
                var accountsTask = _accountRepository.GetAllAsync();
                var budgetsTask = _budgetSummaryService.GetSummaryAsync(
                    SelectedPeriod?.Type ?? PeriodType.CalendarMonth, CustomStartDate, CustomEndDate);
                await Task.WhenAll(balancesTask, incomesTask, expensesTask, entriesTask, accountsTask, budgetsTask);
                cancellationToken.ThrowIfCancellationRequested();

                _accountBalances = await balancesTask;
                var balanceLines = CurrencyBalance.DisplayLines(
                    CurrencyBalance.FromAccounts(_accountBalances, MoneyFormatter.ResolveCurrencyCode(null)));
                BalanceText = balanceLines[0];
                OtherCurrencyBalances = balanceLines.Skip(1).ToList();

                TotalIncomes = await incomesTask;
                TotalExpenses = await expensesTask;
                NetIncome = TotalIncomes - TotalExpenses;
                // Re-format even when the values didn't change: the base currency may have (Settings)
                OnPropertyChanged(nameof(TotalIncomes));
                OnPropertyChanged(nameof(TotalExpenses));
                OnPropertyChanged(nameof(NetIncome));

                // Whole transfers for the Recent card and the other views; split once for the account view, so
                // switching the grouping never reloads
                _entries = await entriesTask;
                _accountEntries = LedgerTransfers.PerAccount(_entries);
                _accounts = await accountsTask;
                RecentTransactions = new(_entries.OrderByDescending(e => e.Date).Take(5));
                Filters.SetOptions(_entries);
                ApplyFilters();

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

        private void ApplyFilters()
        {
            // Only the account view lists a transfer under both of its accounts
            var entries = CurrentGroupingMode == GroupingMode.Account ? _accountEntries : _entries;
            var filter = Filters.ToFilter(IsAscending);
            var filtered = LedgerFilter.Apply(entries, filter);

            TotalCount = entries.Count;
            FilteredCount = filtered.Count;
            HasActiveFilters = filter.IsActive;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (CurrentGroupingMode == GroupingMode.None)
                {
                    Transactions = new(filtered);
                    GroupedTransactions = [];
                }
                else
                {
                    GroupedTransactions = new(LedgerGrouping.Group(filtered, CurrentGroupingMode, _accountBalances, _accounts));
                    Transactions = [];
                }

                HasTransactions = filtered.Count > 0;
            });
        }

        [RelayCommand]
        private Task AddIncomeAsync() => _navigationService.NavigateToAsync(nameof(AddTransactionPage),
            new Dictionary<string, object> { { "TransactionType", TransactionType.Income } });

        [RelayCommand]
        private Task AddExpenseAsync() => _navigationService.NavigateToAsync(nameof(AddTransactionPage),
            new Dictionary<string, object> { { "TransactionType", TransactionType.Expense } });

        [RelayCommand]
        private Task AddTransferAsync() => _navigationService.NavigateToAsync(nameof(AddTransferPage));

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

        [RelayCommand]
        private void ToggleSortOrder()
        {
            IsAscending = !IsAscending;
            ApplyFilters();
        }

        [RelayCommand]
        private void ToggleGrouping()
        {
            // None -> Category -> Account -> None
            CurrentGroupingMode = CurrentGroupingMode switch
            {
                GroupingMode.None => GroupingMode.Category,
                GroupingMode.Category => GroupingMode.Account,
                _ => GroupingMode.None
            };
            ApplyFilters();
        }

        [RelayCommand]
        private void ToggleGroupExpanded(TransactionGroup group) => group?.ToggleExpanded();

        [RelayCommand]
        private async Task EditTransactionAsync(LedgerEntry entry)
        {
            if (entry == null)
                return;

            if (entry.IsTransfer)
            {
                await _navigationService.NavigateToAsync($"{nameof(AddTransferPage)}?TransferId={entry.SourceId}");
                return;
            }

            var transaction = await _transactionRepository.GetByIdAsync(entry.SourceId);
            if (transaction != null)
            {
                await _navigationService.NavigateToAsync(nameof(AddTransactionPage),
                    new Dictionary<string, object> { { "Transaction", transaction } });
            }
        }

        [RelayCommand]
        private async Task DeleteTransactionAsync(LedgerEntry entry)
        {
            if (entry == null)
                return;

            var amount = MoneyFormatter.Format(entry.Amount, entry.CurrencyCode);
            var confirmMessage = entry.IsTransfer
                ? string.Format(AppResources.ConfirmDeleteTransferFromListMessage, entry.Description, amount)
                : string.Format(AppResources.ConfirmDeleteTransactionMessage, entry.Description, amount);

            if (!await _dialogService.ConfirmAsync(AppResources.ConfirmDeleteTransactionTitle, confirmMessage,
                    AppResources.YesDelete, AppResources.Cancel))
                return;

            try
            {
                await _ledgerService.DeleteAsync(entry);
                await LoadDataCommand.ExecuteAsync(null);

                await _dialogService.ToastAsync(entry.IsTransfer
                    ? AppResources.TransferDeletedSuccessfully
                    : AppResources.TransactionDeletedSuccessfully);
            }
            catch (Exception ex)
            {
                var errorMessage = entry.IsTransfer
                    ? AppResources.FailedToDeleteTransfer
                    : AppResources.FailedToDeleteTransaction;
                await _errorHandler.HandleAsync(ex, errorMessage);
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
