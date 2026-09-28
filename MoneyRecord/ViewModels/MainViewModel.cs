using CommunityToolkit.Maui.Alerts;
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
        private readonly ITransactionEnrichmentService _enrichmentService;
        private readonly ITransactionRepository _transactionRepository;
        private readonly ITransferRepository _transferRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly IBudgetRepository _budgetRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly IErrorHandler _errorHandler;

        private List<Transaction> _allCombinedTransactions = new();
        private Dictionary<string, decimal>? _cachedAccountBalances;
        private Dictionary<string, string>? _cachedAccountIcons;

        [ObservableProperty]
        private decimal currentBalance;

        [ObservableProperty]
        private decimal totalIncomes;

        [ObservableProperty]
        private decimal totalExpenses;

        [ObservableProperty]
        private PeriodItem selectedPeriod;

        [ObservableProperty]
        private DateTime currentDate = DateTime.Now;

        [ObservableProperty]
        private DateTime customStartDate = DateTime.Now.AddMonths(-1);

        [ObservableProperty]
        private DateTime customEndDate = DateTime.Now;

        [ObservableProperty]
        private bool isCustomPeriodSelected = false;

        [ObservableProperty]
        private GroupingMode currentGroupingMode = GroupingMode.Category;

        [ObservableProperty]
        private bool isGroupedByCategory = true;

        [ObservableProperty]
        private bool isAscending = false;

        [ObservableProperty]
        private bool isRefreshing = false;

        [ObservableProperty]
        private bool hasTransactions = false;

        [ObservableProperty]
        private ObservableCollection<Transaction> transactions = new();

        [ObservableProperty]
        private ObservableCollection<TransactionGroup> groupedTransactions = new();

        [ObservableProperty]
        private ObservableCollection<AccountBalanceInfo> accountBalances = new();

        [ObservableProperty]
        private ObservableCollection<BudgetProgress> budgetSummaries = new();

        [ObservableProperty]
        private bool hasBudgets = false;

        [ObservableProperty]
        private int budgetsOnTrack;

        [ObservableProperty]
        private int budgetsOverBudget;

        [ObservableProperty]
        private string searchText = string.Empty;

        [ObservableProperty]
        private bool isFilterVisible = false;

        [ObservableProperty]
        private string selectedFilterType = "All";

        [ObservableProperty]
        private string? selectedFilterCategory;

        [ObservableProperty]
        private string? selectedFilterAccount;

        [ObservableProperty]
        private string minAmountText = string.Empty;

        [ObservableProperty]
        private string maxAmountText = string.Empty;

        [ObservableProperty]
        private bool hasActiveFilters = false;

        [ObservableProperty]
        private int filteredCount;

        [ObservableProperty]
        private int totalCount;

        [ObservableProperty]
        private ObservableCollection<string> filterCategories = new();

        [ObservableProperty]
        private ObservableCollection<string> filterAccounts = new();

        public List<string> FilterTypes { get; } = new() { "All", "Income", "Expense", "Transfer" };

        public List<PeriodItem> Periods { get; } = PeriodHelper.GetPeriods();

        public MainViewModel(IBalanceService balanceService, ITransactionEnrichmentService enrichmentService, ITransactionRepository transactionRepository, ITransferRepository transferRepository, IAccountRepository accountRepository, IBudgetRepository budgetRepository, ICategoryRepository categoryRepository, IErrorHandler errorHandler)
        {
            _balanceService = balanceService;
            _enrichmentService = enrichmentService;
            _transactionRepository = transactionRepository;
            _transferRepository = transferRepository;
            _accountRepository = accountRepository;
            _budgetRepository = budgetRepository;
            _categoryRepository = categoryRepository;
            _errorHandler = errorHandler;
            selectedPeriod = PeriodHelper.GetDefaultPeriod();
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

                var (startDate, endDate) = GetDateRange();

                var balanceTask = _balanceService.GetTotalBalanceAsync();
                var incomesTask = _balanceService.GetTotalIncomesAsync(startDate, endDate);
                var expensesTask = _balanceService.GetTotalExpensesAsync(startDate, endDate);
                var transactionsTask = _enrichmentService.GetEnrichedTransactionsAsync(startDate, endDate);
                var transfersTask = _enrichmentService.GetEnrichedTransfersAsync(startDate, endDate);

                await Task.WhenAll(balanceTask, incomesTask, expensesTask, transactionsTask, transfersTask);

                CurrentBalance = await balanceTask;
                TotalIncomes = await incomesTask;
                TotalExpenses = await expensesTask;

                var transactionList = await transactionsTask ?? new List<Transaction>();
                var transfers = await transfersTask ?? new List<Transfer>();
                var transferTransactions = ConvertTransfersToTransactions(transfers, CurrentGroupingMode);

                _allCombinedTransactions = transactionList.Concat(transferTransactions).ToList();

                if (CurrentGroupingMode == GroupingMode.Account)
                {
                    var balanceInfosTask = _balanceService.GetAllAccountBalancesAsync();
                    var accountsTask = _accountRepository.GetAllAsync();
                    await Task.WhenAll(balanceInfosTask, accountsTask);

                    var balanceInfos = await balanceInfosTask ?? new List<AccountBalanceInfo>();
                    _cachedAccountBalances = balanceInfos.ToDictionary(b => b.AccountName ?? string.Empty, b => b.CurrentBalance);

                    var accounts = await accountsTask ?? new List<Account>();
                    _cachedAccountIcons = accounts.ToDictionary(a => a.Name ?? string.Empty, a => a.IconCode ?? "F0070");
                }
                else
                {
                    _cachedAccountBalances = null;
                    _cachedAccountIcons = null;
                }

                UpdateFilterOptions();
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

        private void UpdateFilterOptions()
        {
            var categories = _allCombinedTransactions
                .Select(t => t.CategoryName)
                .Where(n => !string.IsNullOrEmpty(n))
                .Distinct()
                .OrderBy(n => n)
                .ToList();

            var accounts = _allCombinedTransactions
                .Select(t => t.AccountName)
                .Where(n => !string.IsNullOrEmpty(n))
                .Distinct()
                .OrderBy(n => n)
                .ToList();

            FilterCategories = new ObservableCollection<string>(categories!);
            FilterAccounts = new ObservableCollection<string>(accounts!);
        }

        private void ApplyFilters()
        {
            var filtered = _allCombinedTransactions.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var search = SearchText.Trim();
                filtered = filtered.Where(t =>
                    (t.Description?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (t.CategoryName?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (t.AccountName?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            if (SelectedFilterType != "All" && !string.IsNullOrEmpty(SelectedFilterType))
            {
                filtered = SelectedFilterType switch
                {
                    "Income" => filtered.Where(t => t.Type == TransactionType.Income),
                    "Expense" => filtered.Where(t => t.Type == TransactionType.Expense),
                    "Transfer" => filtered.Where(t => t.Type == TransactionType.Transfer),
                    _ => filtered
                };
            }

            if (!string.IsNullOrEmpty(SelectedFilterCategory))
            {
                filtered = filtered.Where(t => t.CategoryName == SelectedFilterCategory);
            }

            if (!string.IsNullOrEmpty(SelectedFilterAccount))
            {
                filtered = filtered.Where(t => t.AccountName == SelectedFilterAccount);
            }

            if (decimal.TryParse(MinAmountText, out var minAmount))
            {
                filtered = filtered.Where(t => t.Amount >= minAmount);
            }

            if (decimal.TryParse(MaxAmountText, out var maxAmount))
            {
                filtered = filtered.Where(t => t.Amount <= maxAmount);
            }

            var combinedList = IsAscending
                ? filtered.OrderBy(t => t.Date).ToList()
                : filtered.OrderByDescending(t => t.Date).ToList();

            TotalCount = _allCombinedTransactions.Count;
            FilteredCount = combinedList.Count;
            HasActiveFilters = !string.IsNullOrWhiteSpace(SearchText) ||
                               (SelectedFilterType != "All" && !string.IsNullOrEmpty(SelectedFilterType)) ||
                               !string.IsNullOrEmpty(SelectedFilterCategory) ||
                               !string.IsNullOrEmpty(SelectedFilterAccount) ||
                               !string.IsNullOrEmpty(MinAmountText) ||
                               !string.IsNullOrEmpty(MaxAmountText);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (CurrentGroupingMode != GroupingMode.None)
                {
                    IEnumerable<IGrouping<string, Transaction>> groupedList;

                    if (CurrentGroupingMode == GroupingMode.Category)
                    {
                        groupedList = combinedList
                            .Where(t => !string.IsNullOrEmpty(t.CategoryName))
                            .GroupBy(t => t.CategoryName ?? string.Empty);
                    }
                    else
                    {
                        groupedList = combinedList
                            .Where(t => !string.IsNullOrEmpty(t.AccountName))
                            .GroupBy(t => t.AccountName ?? string.Empty);
                    }

                    var groups = new List<TransactionGroup>();

                    foreach (var g in groupedList)
                    {
                        decimal? overrideTotal = null;
                        string? accountIconCode = null;

                        if (CurrentGroupingMode == GroupingMode.Account)
                        {
                            if (_cachedAccountBalances != null)
                                overrideTotal = _cachedAccountBalances.GetValueOrDefault(g.Key, 0);
                            if (_cachedAccountIcons != null)
                                accountIconCode = _cachedAccountIcons.GetValueOrDefault(g.Key, "F0070");
                        }

                        groups.Add(new TransactionGroup(g.Key, g.ToList(), CurrentGroupingMode, overrideTotal, accountIconCode));
                    }

                    groups = groups.OrderBy(g => g.GroupName).ToList();
                    GroupedTransactions = new ObservableCollection<TransactionGroup>(groups);
                    OnPropertyChanged(nameof(GroupedTransactions));
                    Transactions.Clear();
                }
                else
                {
                    Transactions.Clear();
                    foreach (var transaction in combinedList)
                        Transactions.Add(transaction);
                    GroupedTransactions.Clear();
                }

                HasTransactions = combinedList.Any();
            });
        }

        [RelayCommand]
        private async Task AddIncomeAsync()
        {
            await Shell.Current.GoToAsync(nameof(AddTransactionPage), new Dictionary<string, object>
            {
                { "TransactionType", TransactionType.Income }
            });
        }

        [RelayCommand]
        private async Task AddExpenseAsync()
        {
            await Shell.Current.GoToAsync(nameof(AddTransactionPage), new Dictionary<string, object>
            {
                { "TransactionType", TransactionType.Expense }
            });
        }

        [RelayCommand]
        private async Task AddTransferAsync()
        {
            await Shell.Current.GoToAsync(nameof(AddTransferPage));
        }

        [RelayCommand]
        private async Task ManageIncomeCategoriesAsync()
        {
            await Shell.Current.GoToAsync("//ManageIncomeCategories");
        }

        [RelayCommand]
        private async Task ManageExpenseCategoriesAsync()
        {
            await Shell.Current.GoToAsync("//ManageExpenseCategories");
        }

        [RelayCommand]
        private async Task ManageAccountsAsync()
        {
            await Shell.Current.GoToAsync("//ManageAccounts");
        }

        [RelayCommand]
        private async Task ShowAccountBalancesAsync()
        {
            try
            {
                var balances = await _balanceService.GetAllAccountBalancesAsync();

                // Build the message to display
                var message = string.Join("\n\n", balances.Select(b => 
                    $"🏦 {b.AccountName}\n" +
                    $"   {AppResources.Balance}: ${b.CurrentBalance:N2}\n" +
                    $"   {AppResources.LastActivity}: {b.LastActivityDateDisplay}"));

                if (string.IsNullOrEmpty(message))
                {
                    message = AppResources.NoAccountsFound;
                }

                await Shell.Current.DisplayAlertAsync(AppResources.AccountBalances, message, AppResources.OK);
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, string.Format(AppResources.FailedToLoadAccountBalances, ex.Message));
            }
        }

        [RelayCommand]
        private async Task ToggleSortOrderAsync()
        {
            IsAscending = !IsAscending;
            await LoadDataAsync();
        }

        [RelayCommand]
        private async Task ToggleGroupingAsync()
        {
            try
            {
                // Cycle through: None -> Category -> Account -> None
                CurrentGroupingMode = CurrentGroupingMode switch
                {
                    GroupingMode.None => GroupingMode.Category,
                    GroupingMode.Category => GroupingMode.Account,
                    GroupingMode.Account => GroupingMode.None,
                    _ => GroupingMode.None
                };
                
                // Update legacy property for backward compatibility
                IsGroupedByCategory = CurrentGroupingMode != GroupingMode.None;
                
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, string.Format(AppResources.FailedToToggleView, ex.Message));
            }
        }

        [RelayCommand]
        private void ToggleGroupExpanded(TransactionGroup group)
        {
            if (group != null)
            {
                group.ToggleExpanded();
            }
        }

        [RelayCommand]
        private async Task EditTransactionAsync(Transaction transaction)
        {
            if (transaction == null)
                return;

            // Check if this is a transfer (has TransferId set)
            if (transaction.TransferId.HasValue)
            {
                // Navigate to edit transfer page - convert to string for Shell query property
                await Shell.Current.GoToAsync($"{nameof(AddTransferPage)}?TransferId={transaction.TransferId.Value}");
            }
            else
            {
                await Shell.Current.GoToAsync(nameof(AddTransactionPage), new Dictionary<string, object>
                {
                    { "Transaction", transaction }
                });
            }
        }

        [RelayCommand]
        private async Task DeleteTransactionAsync(Transaction transaction)
        {
            if (transaction == null)
                return;

            // Check if this is a transfer
            var isTransfer = transaction.TransferId.HasValue;

            var confirmMessage = isTransfer 
                ? string.Format(AppResources.ConfirmDeleteTransferFromListMessage, transaction.Description, transaction.Amount)
                : string.Format(AppResources.ConfirmDeleteTransactionMessage, transaction.Description, transaction.Amount);

            var confirm = await Shell.Current.DisplayAlertAsync(
                AppResources.ConfirmDeleteTransactionTitle,
                confirmMessage,
                AppResources.YesDelete,
                AppResources.Cancel);

            if (!confirm)
                return;

            try
            {
                if (isTransfer)
                {
                    // Delete the transfer
                    var transfer = await _transferRepository.GetByIdAsync(transaction.TransferId!.Value);
                    if (transfer != null)
                    {
                        await _transferRepository.DeleteAsync(transfer);
                    }
                }
                else
                {
                    await _transactionRepository.DeleteAsync(transaction);
                }
                
                await LoadDataAsync();

                var successMessage = isTransfer ? AppResources.TransferDeletedSuccessfully : AppResources.TransactionDeletedSuccessfully;
                await Toast.Make(successMessage).Show();
            }
            catch (Exception ex)
            {
                var errorMessage = isTransfer
                    ? string.Format(AppResources.FailedToDeleteTransfer, ex.Message)
                    : string.Format(AppResources.FailedToDeleteTransaction, ex.Message);
                await _errorHandler.HandleAsync(ex, errorMessage);
            }
        }

        [RelayCommand]
        private void ToggleFilters()
        {
            IsFilterVisible = !IsFilterVisible;
        }

        [RelayCommand]
        private void ClearFilters()
        {
            SearchText = string.Empty;
            SelectedFilterType = "All";
            SelectedFilterCategory = null;
            SelectedFilterAccount = null;
            MinAmountText = string.Empty;
            MaxAmountText = string.Empty;
            ApplyFilters();
        }

        [RelayCommand]
        private void PerformSearch()
        {
            ApplyFilters();
        }

        partial void OnSearchTextChanged(string value)
        {
            ApplyFilters();
        }

        partial void OnSelectedFilterTypeChanged(string value)
        {
            ApplyFilters();
        }

        partial void OnSelectedFilterCategoryChanged(string? value)
        {
            ApplyFilters();
        }

        partial void OnSelectedFilterAccountChanged(string? value)
        {
            ApplyFilters();
        }

        partial void OnMinAmountTextChanged(string value)
        {
            ApplyFilters();
        }

        partial void OnMaxAmountTextChanged(string value)
        {
            ApplyFilters();
        }

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

        private async Task LoadBudgetSummaryAsync()
        {
            var activeBudgets = await _budgetRepository.GetActiveBudgetsAsync();
            if (activeBudgets.Count == 0)
            {
                HasBudgets = false;
                BudgetSummaries = new ObservableCollection<BudgetProgress>();
                return;
            }

            var now = DateTime.Now;
            var monthStart = new DateTime(now.Year, now.Month, 1);
            var monthEnd = monthStart.AddMonths(1).AddTicks(-1);

            var progressList = new List<BudgetProgress>();

            foreach (var budget in activeBudgets)
            {
                var category = await _categoryRepository.GetByIdAsync(budget.CategoryId);
                if (category == null) continue;

                var (start, end) = budget.Period switch
                {
                    BudgetPeriod.Day => (now.Date, now.Date.AddDays(1).AddTicks(-1)),
                    BudgetPeriod.Month => (monthStart, monthEnd),
                    BudgetPeriod.Year => (new DateTime(now.Year, 1, 1), new DateTime(now.Year, 12, 31, 23, 59, 59)),
                    _ => (monthStart, monthEnd)
                };

                var spent = await _balanceService.GetCategoryExpensesAsync(budget.CategoryId, start, end);

                var progress = new BudgetProgress
                {
                    BudgetId = budget.Id,
                    CategoryId = budget.CategoryId,
                    CategoryName = category.Name,
                    CategoryIconCode = category.IconCode,
                    Period = budget.Period,
                    OriginalLimitAmount = budget.LimitAmount,
                    LimitAmount = budget.LimitAmount,
                    SpentAmount = spent
                };
                progress.CalculateProgress();
                progressList.Add(progress);
            }

            var sorted = progressList.OrderByDescending(b => b.ProgressPercentage).ToList();

            BudgetsOnTrack = sorted.Count(b => !b.IsOverBudget);
            BudgetsOverBudget = sorted.Count(b => b.IsOverBudget);
            BudgetSummaries = new ObservableCollection<BudgetProgress>(sorted.Take(3));
            HasBudgets = true;
        }

        private (DateTime startDate, DateTime endDate) GetDateRange()
        {
            return DateRangeHelper.GetDateRange(SelectedPeriod?.Type, CustomStartDate, CustomEndDate);
        }

        /// <summary>
        /// Converts Transfer objects to Transaction objects for unified display.
        /// When grouping by Category: creates one transaction per transfer with "Transfer" category.
        /// When grouping by Account: creates two transactions per transfer (outgoing and incoming).
        /// When not grouped: creates one transaction per transfer showing the transfer details.
        /// </summary>
        private List<Transaction> ConvertTransfersToTransactions(List<Transfer> transfers, GroupingMode groupingMode)
        {
            var result = new List<Transaction>();
            const string transferCategoryName = "Transfers";
            const string transferIconCode = "F0A27"; // bank-transfer icon




            foreach (var transfer in transfers)
            {
                if (groupingMode == GroupingMode.Account)
                {
                    // Create outgoing transaction for source account (negative)
                    result.Add(new Transaction
                    {
                        Id = -transfer.Id, // Use negative ID to avoid conflicts with real transactions
                        Date = transfer.Date,
                        Description = $"Transfer to {transfer.DestinationAccountName}: {transfer.Description}",
                        Amount = transfer.Amount,
                        Type = TransactionType.Transfer,
                        AccountId = transfer.SourceAccountId,
                        AccountName = transfer.SourceAccountName,
                        AccountIconCode = transferIconCode,
                        CategoryName = transferCategoryName,
                        CategoryIconCode = transferIconCode,
                        TransferId = transfer.Id,
                        IsOutgoingTransfer = true,
                        TransferCounterpartAccount = transfer.DestinationAccountName
                    });

                    // Create incoming transaction for destination account (positive)
                    result.Add(new Transaction
                    {
                        Id = -transfer.Id - 1000000, // Use offset to ensure unique ID
                        Date = transfer.Date,
                        Description = $"Transfer from {transfer.SourceAccountName}: {transfer.Description}",
                        Amount = transfer.Amount,
                        Type = TransactionType.Transfer,
                        AccountId = transfer.DestinationAccountId,
                        AccountName = transfer.DestinationAccountName,
                        AccountIconCode = transferIconCode,
                        CategoryName = transferCategoryName,
                        CategoryIconCode = transferIconCode,
                        TransferId = transfer.Id,
                        IsOutgoingTransfer = false,
                        TransferCounterpartAccount = transfer.SourceAccountName
                    });
                }
                else
                {
                    // For Category grouping or flat list: single entry showing the transfer
                    result.Add(new Transaction
                    {
                        Id = -transfer.Id,
                        Date = transfer.Date,
                        Description = string.IsNullOrEmpty(transfer.Description) 
                            ? $"{transfer.SourceAccountName} → {transfer.DestinationAccountName}"
                            : transfer.Description,
                        Amount = transfer.Amount,
                        Type = TransactionType.Transfer,
                        AccountId = transfer.SourceAccountId,
                        AccountName = $"{transfer.SourceAccountName} → {transfer.DestinationAccountName}",
                        AccountIconCode = transferIconCode,
                        CategoryName = transferCategoryName,
                        CategoryIconCode = transferIconCode,
                        TransferId = transfer.Id,
                        IsOutgoingTransfer = false,
                        TransferCounterpartAccount = transfer.DestinationAccountName
                    });
                }
            }

            return result;
        }
    }
}
