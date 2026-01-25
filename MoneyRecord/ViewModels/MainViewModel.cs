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
        private bool isGroupedByCategory = true;  // Default to grouped view

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

                CurrentBalance = await _databaseService.GetBalanceAsync(startDate, endDate);
                TotalIncomes = await _databaseService.GetTotalIncomesAsync(startDate, endDate);
                TotalExpenses = await _databaseService.GetTotalExpensesAsync(startDate, endDate);

                var transactionList = await _databaseService.GetTransactionsAsync(startDate, endDate);
                
                // Sort by date
                transactionList = IsAscending 
                    ? transactionList.OrderBy(t => t.Date).ToList()
                    : transactionList.OrderByDescending(t => t.Date).ToList();

                // Update collections on main thread
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    
                    if (IsGroupedByCategory)
                    {
                        // Group transactions by category with null safety
                        var validTransactions = transactionList
                            .Where(t => !string.IsNullOrEmpty(t.CategoryName))
                            .ToList();
                            
                        var groupedList = validTransactions
                            .GroupBy(t => t.CategoryName)
                            .ToList();
                            
                        var groups = new List<TransactionGroup>();
                            
                        foreach (var g in groupedList)
                        {
                            var transactionsInGroup = g.ToList();
                                
                            try
                            {
                                var group = new TransactionGroup(g.Key, transactionsInGroup);
                                groups.Add(group);
                            }
                            catch (Exception ex)
                            {
                                throw;
                            }
                        }
                            
                        groups = groups.OrderBy(g => g.CategoryName).ToList();

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
                        foreach (var transaction in transactionList)
                        {
                            Transactions.Add(transaction);
                        }

                        GroupedTransactions.Clear();
                    }
                    
                    // Update HasTransactions flag
                    HasTransactions = transactionList.Any();
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
                IsGroupedByCategory = !IsGroupedByCategory;
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

            await Shell.Current.GoToAsync(nameof(AddTransactionPage), new Dictionary<string, object>
            {
                { "Transaction", transaction }
            });
        }

        [RelayCommand]
        private async Task DeleteTransactionAsync(Transaction transaction)
        {
            if (transaction == null)
                return;

            var confirm = await Shell.Current.DisplayAlert(
                "Confirm Delete",
                $"Are you sure you want to delete this transaction?\n\n{transaction.Description}\n${transaction.Amount:N2}",
                "Yes, Delete",
                "Cancel");

            if (!confirm)
                return;

            try
            {
                await _databaseService.DeleteTransactionAsync(transaction);
                await LoadDataAsync();
                
                await Shell.Current.DisplayAlert("Success", "Transaction deleted successfully", "OK");
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", $"Failed to delete transaction: {ex.Message}", "OK");
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
    }
}
