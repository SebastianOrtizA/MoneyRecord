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
        private readonly ILedgerQueryService _ledgerQueryService;
        private readonly IBudgetSummaryService _budgetSummaryService;
        private readonly ITransactionRepository _transactionRepository;
        private readonly ITransferRepository _transferRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly IErrorHandler _errorHandler;
        private readonly IDialogService _dialogService;
        private readonly INavigationService _navigationService;
        private readonly IAppPaths _paths;

        private List<LedgerEntry> _entries = [];
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
        private string searchText = string.Empty;

        [ObservableProperty]
        private bool isFilterVisible;

        [ObservableProperty]
        private TypeFilterOption? selectedFilterType;

        [ObservableProperty]
        private FilterOption? selectedFilterCategory;

        [ObservableProperty]
        private FilterOption? selectedFilterAccount;

        [ObservableProperty]
        private string minAmountText = string.Empty;

        [ObservableProperty]
        private string maxAmountText = string.Empty;

        [ObservableProperty]
        private bool hasActiveFilters;

        [ObservableProperty]
        private int filteredCount;

        [ObservableProperty]
        private int totalCount;

        [ObservableProperty]
        private List<FilterOption> filterCategories = [];

        [ObservableProperty]
        private List<FilterOption> filterAccounts = [];

        public List<TypeFilterOption> FilterTypes { get; } =
        [
            new(null, AppResources.All),
            new(TransactionType.Income, AppResources.Incomes),
            new(TransactionType.Expense, AppResources.Expenses),
            new(TransactionType.Transfer, AppResources.Transfers)
        ];

        public List<PeriodItem> Periods { get; } = PeriodHelper.GetPeriods();

        public MainViewModel(IBalanceService balanceService, ILedgerQueryService ledgerQueryService, IBudgetSummaryService budgetSummaryService, ITransactionRepository transactionRepository, ITransferRepository transferRepository, IAccountRepository accountRepository, IErrorHandler errorHandler, IDialogService dialogService, INavigationService navigationService, IAppPaths paths)
        {
            _balanceService = balanceService;
            _ledgerQueryService = ledgerQueryService;
            _budgetSummaryService = budgetSummaryService;
            _transactionRepository = transactionRepository;
            _transferRepository = transferRepository;
            _accountRepository = accountRepository;
            _errorHandler = errorHandler;
            _dialogService = dialogService;
            _navigationService = navigationService;
            _paths = paths;
            selectedPeriod = PeriodHelper.GetDefaultPeriod();
            selectedFilterType = FilterTypes[0];
        }

        public async Task InitializeAsync()
        {
            await LoadDataAsync();
        }

        [RelayCommand]
        private async Task LoadDataAsync()
        {
            try
            {
                IsRefreshing = true;

                var (startDate, endDate) = DateRangeHelper.GetDateRange(SelectedPeriod?.Type, CustomStartDate, CustomEndDate);
                var byAccount = CurrentGroupingMode == GroupingMode.Account;

                var balancesTask = _balanceService.GetAllAccountBalancesAsync();
                var incomesTask = _balanceService.GetTotalIncomesAsync(startDate, endDate);
                var expensesTask = _balanceService.GetTotalExpensesAsync(startDate, endDate);
                var entriesTask = _ledgerQueryService.GetEntriesAsync(startDate, endDate, transfersPerAccount: byAccount);
                await Task.WhenAll(balancesTask, incomesTask, expensesTask, entriesTask);

                _accountBalances = await balancesTask;
                ShowBalances(CurrencyBalance.FromAccounts(_accountBalances, MoneyFormatter.ResolveCurrencyCode(null)));
                TotalIncomes = await incomesTask;
                TotalExpenses = await expensesTask;
                NetIncome = TotalIncomes - TotalExpenses;
                // Re-format even when the values didn't change: the base currency may have (Settings)
                OnPropertyChanged(nameof(TotalIncomes));
                OnPropertyChanged(nameof(TotalExpenses));
                OnPropertyChanged(nameof(NetIncome));

                _entries = await entriesTask;
                _accounts = byAccount ? await _accountRepository.GetAllAsync() : [];
                RecentTransactions = new(_entries.OrderByDescending(e => e.Date).Take(5));
                FilterCategories = LedgerFilter.CategoryOptions(_entries);
                FilterAccounts = LedgerFilter.AccountOptions(_entries);
                ApplyFilters();

                await LoadBudgetSummaryAsync();
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, string.Format(AppResources.FailedToLoadTransactions, ex.Message));
            }
            finally
            {
                IsRefreshing = false;
            }
        }

        private void ShowBalances(List<CurrencyBalance> balances)
        {
            if (balances.Count <= 1)
            {
                BalanceText = balances.Count == 1
                    ? MoneyFormatter.Format(balances[0].Balance, balances[0].CurrencyCode)
                    : MoneyFormatter.Format(0);
                OtherCurrencyBalances = [];
                return;
            }

            var lines = balances.Select(b => $"{MoneyFormatter.Format(b.Balance, b.CurrencyCode)} {b.CurrencyCode}").ToList();
            BalanceText = lines[0];
            OtherCurrencyBalances = lines.Skip(1).ToList();
        }

        /// <summary>The budget card uses the Home period, like the Budget page does with its own.</summary>
        private async Task LoadBudgetSummaryAsync()
        {
            var summary = await _budgetSummaryService.GetSummaryAsync(
                SelectedPeriod?.Type ?? PeriodType.CalendarMonth, CustomStartDate, CustomEndDate);

            BudgetsOnTrack = summary.Count(b => !b.IsOverBudget);
            BudgetsOverBudget = summary.Count(b => b.IsOverBudget);
            BudgetSummaries = new(summary.Take(3));
            HasBudgets = summary.Count > 0;
        }

        private TransactionFilter CurrentFilter() => new(
            SearchText,
            SelectedFilterType?.Type,
            SelectedFilterCategory?.Id,
            SelectedFilterAccount?.Id,
            ParseAmountLimit(MinAmountText),
            ParseAmountLimit(MaxAmountText),
            IsAscending);

        /// <summary>Empty = no limit; otherwise read like amount entry (base currency).</summary>
        private static decimal? ParseAmountLimit(string text) =>
            string.IsNullOrWhiteSpace(text) ? null : AmountParser.Parse(text, null);

        private void ApplyFilters()
        {
            var filter = CurrentFilter();
            var filtered = LedgerFilter.Apply(_entries, filter);

            TotalCount = _entries.Count;
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
                await _errorHandler.HandleAsync(ex, string.Format(AppResources.FailedToLoadAccountBalances, ex.Message));
            }
        }

        [RelayCommand]
        private void ToggleSortOrder()
        {
            IsAscending = !IsAscending;
            ApplyFilters();
        }

        [RelayCommand]
        private async Task ToggleGroupingAsync()
        {
            // None -> Category -> Account -> None
            var previous = CurrentGroupingMode;
            CurrentGroupingMode = previous switch
            {
                GroupingMode.None => GroupingMode.Category,
                GroupingMode.Category => GroupingMode.Account,
                _ => GroupingMode.None
            };

            // Only the account view splits transfers into two rows, so only entering or leaving it reloads
            if (previous == GroupingMode.Account || CurrentGroupingMode == GroupingMode.Account)
                await LoadDataAsync();
            else
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
                if (entry.IsTransfer)
                {
                    var transfer = await _transferRepository.GetByIdAsync(entry.SourceId);
                    if (transfer != null)
                        await _transferRepository.DeleteAsync(transfer);
                }
                else
                {
                    var transaction = await _transactionRepository.GetByIdAsync(entry.SourceId);
                    if (transaction != null)
                    {
                        await _transactionRepository.DeleteAsync(transaction);
                        ReceiptFiles.Delete(transaction.ReceiptPhotoPath, _paths.ReceiptsFolder);
                    }
                }

                await LoadDataAsync();

                await _dialogService.ToastAsync(entry.IsTransfer
                    ? AppResources.TransferDeletedSuccessfully
                    : AppResources.TransactionDeletedSuccessfully);
            }
            catch (Exception ex)
            {
                var errorMessage = entry.IsTransfer
                    ? string.Format(AppResources.FailedToDeleteTransfer, ex.Message)
                    : string.Format(AppResources.FailedToDeleteTransaction, ex.Message);
                await _errorHandler.HandleAsync(ex, errorMessage);
            }
        }

        [RelayCommand]
        private void ToggleFilters() => IsFilterVisible = !IsFilterVisible;

        [RelayCommand]
        private void ClearFilters()
        {
            SearchText = string.Empty;
            SelectedFilterType = FilterTypes[0];
            SelectedFilterCategory = null;
            SelectedFilterAccount = null;
            MinAmountText = string.Empty;
            MaxAmountText = string.Empty;
            ApplyFilters();
        }

        [RelayCommand]
        private void PerformSearch() => ApplyFilters();

        partial void OnSearchTextChanged(string value) => ApplyFilters();

        partial void OnSelectedFilterTypeChanged(TypeFilterOption? value) => ApplyFilters();

        partial void OnSelectedFilterCategoryChanged(FilterOption? value) => ApplyFilters();

        partial void OnSelectedFilterAccountChanged(FilterOption? value) => ApplyFilters();

        partial void OnMinAmountTextChanged(string value) => ApplyFilters();

        partial void OnMaxAmountTextChanged(string value) => ApplyFilters();

        partial void OnSelectedPeriodChanged(PeriodItem value)
        {
            IsCustomPeriodSelected = value?.Type == PeriodType.CustomPeriod;
            _ = LoadDataAsync();
        }

        partial void OnCustomStartDateChanged(DateTime value)
        {
            if (IsCustomPeriodSelected)
                _ = LoadDataAsync();
        }

        partial void OnCustomEndDateChanged(DateTime value)
        {
            if (IsCustomPeriodSelected)
                _ = LoadDataAsync();
        }
    }
}
