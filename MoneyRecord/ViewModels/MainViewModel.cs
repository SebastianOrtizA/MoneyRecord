using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyRecord.Models;
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
        private string selectedPeriod = "Last Month";

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

        public List<string> Periods { get; } = new() { "Last Week", "Last Month", "Last Year", "Custom Period" };

        public MainViewModel(DatabaseService databaseService)
        {
            _databaseService = databaseService;
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

                // Get total balance across all accounts
                CurrentBalance = await _databaseService.GetTotalBalanceAsync();
                TotalIncomes = await _databaseService.GetTotalIncomesAsync(startDate, endDate);
                TotalExpenses = await _databaseService.GetTotalExpensesAsync(startDate, endDate);

                var transactionList = await _databaseService.GetTransactionsAsync(startDate, endDate) ?? new List<Transaction>();
                
                // Fetch transfers and convert them to Transaction items for display
                var transfers = await _databaseService.GetTransfersAsync(startDate, endDate) ?? new List<Transfer>();
                var transferTransactions = ConvertTransfersToTransactions(transfers, CurrentGroupingMode);
                
                // Combine regular transactions with transfer transactions
                var combinedList = transactionList.Concat(transferTransactions).ToList();
                
                // Pre-fetch account balances if grouping by account
                Dictionary<string, decimal>? accountBalances = null;
                if (CurrentGroupingMode == GroupingMode.Account)
                {
                    var balanceInfos = await _databaseService.GetAllAccountBalancesAsync() ?? new List<AccountBalanceInfo>();
                    accountBalances = balanceInfos.ToDictionary(b => b.AccountName ?? string.Empty, b => b.CurrentBalance);
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
                                if (CurrentGroupingMode == GroupingMode.Account && accountBalances != null)
                                {
                                    overrideTotal = accountBalances.GetValueOrDefault(g.Key, 0);
                                }
                                
                                var group = new TransactionGroup(g.Key, transactionsInGroup, CurrentGroupingMode, overrideTotal);
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
                    await Shell.Current.DisplayAlert("Error", $"Failed to load transactions: {ex.Message}\n\nCheck Output window for details.", "OK");
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
            await Shell.Current.GoToAsync(nameof(ManageCategoriesPage), new Dictionary<string, object>
            {
                { "CategoryType", CategoryType.Income }
            });
        }

        [RelayCommand]
        private async Task ManageExpenseCategoriesAsync()
        {
            await Shell.Current.GoToAsync(nameof(ManageCategoriesPage), new Dictionary<string, object>
            {
                { "CategoryType", CategoryType.Expense }
            });
        }

        [RelayCommand]
        private async Task ManageAccountsAsync()
        {
            await Shell.Current.GoToAsync(nameof(ManageAccountsPage));
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
                    $"   Balance: ${b.CurrentBalance:N2}\n" +
                    $"   Last Activity: {b.LastTransactionDateDisplay}"));

                if (string.IsNullOrEmpty(message))
                {
                    message = "No accounts found.";
                }

                await Shell.Current.DisplayAlert("Account Balances", message, "OK");
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", $"Failed to load account balances: {ex.Message}", "OK");
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
                    await Shell.Current.DisplayAlert("Error", $"Failed to toggle view: {ex.Message}", "OK");
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

            var confirm = await Shell.Current.DisplayAlert(
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
                
                await Shell.Current.DisplayAlert("Success", $"{char.ToUpper(itemType[0])}{itemType[1..]} deleted successfully", "OK");
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", $"Failed to delete {itemType}: {ex.Message}", "OK");
            }
        }

        partial void OnSelectedPeriodChanged(string value)
        {
            IsCustomPeriodSelected = value == "Custom Period";
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
            var endDate = DateTime.Now.Date.AddDays(1).AddTicks(-1);
            DateTime startDate;

            switch (SelectedPeriod)
            {
                case "Last Week":
                    startDate = DateTime.Now.Date.AddDays(-7);
                    break;
                case "Last Year":
                    startDate = DateTime.Now.Date.AddYears(-1);
                    break;
                case "Custom Period":
                    startDate = CustomStartDate.Date;
                    endDate = CustomEndDate.Date.AddDays(1).AddTicks(-1);
                    break;
                default: // Last Month
                    startDate = DateTime.Now.Date.AddMonths(-1);
                    break;
            }

            return (startDate, endDate);
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
            const string transferCategoryName = "🔄 Transfers";

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
                        CategoryName = transferCategoryName,
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
                        CategoryName = transferCategoryName,
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
                        CategoryName = transferCategoryName,
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
