using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services;
using MoneyRecord.Views;
using System.Collections.ObjectModel;

namespace MoneyRecord.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;

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
        private GroupingMode currentGroupingMode = GroupingMode.Category;  // Default to grouped by category

        [ObservableProperty]
        private bool isGroupedByCategory = true;  // Kept for backward compatibility

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

        public List<PeriodItem> Periods { get; } = PeriodHelper.GetPeriods();

        public MainViewModel(DatabaseService databaseService)
        {
            _databaseService = databaseService;
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

                // Execute all database queries in parallel for faster loading
                var balanceTask = _databaseService.GetTotalBalanceAsync();
                var incomesTask = _databaseService.GetTotalIncomesAsync(startDate, endDate);
                var expensesTask = _databaseService.GetTotalExpensesAsync(startDate, endDate);
                var transactionsTask = _databaseService.GetTransactionsAsync(startDate, endDate);
                var transfersTask = _databaseService.GetTransfersAsync(startDate, endDate);

                // Wait for all queries to complete
                await Task.WhenAll(balanceTask, incomesTask, expensesTask, transactionsTask, transfersTask);

                // Retrieve results
                CurrentBalance = await balanceTask;
                TotalIncomes = await incomesTask;
                TotalExpenses = await expensesTask;

                var transactionList = await transactionsTask ?? new List<Transaction>();

                // Fetch transfers and convert them to Transaction items for display
                var transfers = await transfersTask ?? new List<Transfer>();
                var transferTransactions = ConvertTransfersToTransactions(transfers, CurrentGroupingMode);

                // Combine regular transactions with transfer transactions
                var combinedList = transactionList.Concat(transferTransactions).ToList();

                // Pre-fetch account balances and icons if grouping by account
                Dictionary<string, decimal>? accountBalances = null;
                Dictionary<string, string>? accountIcons = null;
                if (CurrentGroupingMode == GroupingMode.Account)
                {
                    var balanceInfosTask = _databaseService.GetAllAccountBalancesAsync();
                    var accountsTask = _databaseService.GetAccountsAsync();

                    await Task.WhenAll(balanceInfosTask, accountsTask);

                    var balanceInfos = await balanceInfosTask ?? new List<AccountBalanceInfo>();
                    accountBalances = balanceInfos.ToDictionary(b => b.AccountName ?? string.Empty, b => b.CurrentBalance);

                    // Get account icons
                    var accounts = await accountsTask ?? new List<Account>();
                    accountIcons = accounts.ToDictionary(a => a.Name ?? string.Empty, a => a.IconCode ?? "F0070");
                }
                
                // Sort by date
                combinedList = IsAscending 
                    ? combinedList.OrderBy(t => t.Date).ToList()
                    : combinedList.OrderByDescending(t => t.Date).ToList();

                // Update collections on main thread
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    
                    if (CurrentGroupingMode != GroupingMode.None)
                    {
                        // Group transactions by category or account
                        IEnumerable<IGrouping<string, Transaction>> groupedList;
                        
                        if (CurrentGroupingMode == GroupingMode.Category)
                        {
                            var validTransactions = combinedList
                                .Where(t => !string.IsNullOrEmpty(t.CategoryName))
                                .ToList();
                            groupedList = validTransactions.GroupBy(t => t.CategoryName ?? string.Empty);
                        }
                        else // GroupingMode.Account
                        {
                            var validTransactions = combinedList
                                .Where(t => !string.IsNullOrEmpty(t.AccountName))
                                .ToList();
                            groupedList = validTransactions.GroupBy(t => t.AccountName ?? string.Empty);
                        }
                            
                        var groups = new List<TransactionGroup>();
                            
                        foreach (var g in groupedList)
                        {
                            var transactionsInGroup = g.ToList();
                                
                            try
                            {
                                // For account grouping, use the account balance; for category, sum transactions
                                decimal? overrideTotal = null;
                                string? accountIconCode = null;
                                
                                if (CurrentGroupingMode == GroupingMode.Account)
                                {
                                    if (accountBalances != null)
                                    {
                                        overrideTotal = accountBalances.GetValueOrDefault(g.Key, 0);
                                    }
                                    if (accountIcons != null)
                                    {
                                        accountIconCode = accountIcons.GetValueOrDefault(g.Key, "F0070");
                                    }
                                }
                                
                                var group = new TransactionGroup(g.Key, transactionsInGroup, CurrentGroupingMode, overrideTotal, accountIconCode);
                                groups.Add(group);
                            }
                            catch (Exception ex)
                            {
                                throw;
                            }
                        }
                            
                        groups = groups.OrderBy(g => g.GroupName).ToList();

                        GroupedTransactions.Clear();
                            
                        // Instead of adding one by one, replace the entire collection
                        var newGroupedCollection = new ObservableCollection<TransactionGroup>(groups);
                            
                        GroupedTransactions = newGroupedCollection;
                        OnPropertyChanged(nameof(GroupedTransactions));
                            
                        Transactions.Clear();
                    }
                    else
                    {
                        // Show flat list
                        Transactions.Clear();
                        foreach (var transaction in combinedList)
                        {
                            Transactions.Add(transaction);
                        }

                        GroupedTransactions.Clear();
                    }
                    
                    // Update HasTransactions flag
                    HasTransactions = combinedList.Any();
                });
            }
            catch (Exception ex)
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await Shell.Current.DisplayAlertAsync("Error", $"Failed to load transactions: {ex.Message}\n\nCheck Output window for details.", "OK");
                });
            }
            finally
            {
                IsRefreshing = false;
            }
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
                var balances = await _databaseService.GetAllAccountBalancesAsync();

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
                await Shell.Current.DisplayAlertAsync(AppResources.Error, string.Format(AppResources.FailedToLoadAccountBalances, ex.Message), AppResources.OK);
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
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await Shell.Current.DisplayAlertAsync(AppResources.Error, string.Format(AppResources.FailedToToggleView, ex.Message), AppResources.OK);
                });
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
            var itemType = isTransfer ? "transfer" : "transaction";

            var confirm = await Shell.Current.DisplayAlertAsync(
                "Confirm Delete",
                $"Are you sure you want to delete this {itemType}?\n\n{transaction.Description}\n${transaction.Amount:N2}",
                "Yes, Delete",
                "Cancel");

            if (!confirm)
                return;

            try
            {
                if (isTransfer)
                {
                    // Delete the transfer
                    var transfer = await _databaseService.GetTransferAsync(transaction.TransferId!.Value);
                    if (transfer != null)
                    {
                        await _databaseService.DeleteTransferAsync(transfer);
                    }
                }
                else
                {
                    await _databaseService.DeleteTransactionAsync(transaction);
                }
                
                await LoadDataAsync();
                
                await Shell.Current.DisplayAlertAsync("Success", $"{char.ToUpper(itemType[0])}{itemType[1..]} deleted successfully", "OK");
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlertAsync("Error", $"Failed to delete {itemType}: {ex.Message}", "OK");
            }
        }

        partial void OnSelectedPeriodChanged(PeriodItem value)
        {
            IsCustomPeriodSelected = value?.Type == PeriodType.CustomPeriod;
            _ = LoadDataAsync();
        }

        partial void OnCustomStartDateChanged(DateTime value)
        {
            if (IsCustomPeriodSelected)
            {
                _ = LoadDataAsync();
            }
        }

        partial void OnCustomEndDateChanged(DateTime value)
        {
            if (IsCustomPeriodSelected)
            {
                _ = LoadDataAsync();
            }
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
