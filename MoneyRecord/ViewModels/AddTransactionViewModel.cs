using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyRecord.Behaviors;
using MoneyRecord.Models;
using MoneyRecord.Services;

namespace MoneyRecord.ViewModels
{
    [QueryProperty(nameof(TransactionType), "TransactionType")]
    [QueryProperty(nameof(Transaction), "Transaction")]
    public partial class AddTransactionViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;

        [ObservableProperty]
        private TransactionType transactionType;

        [ObservableProperty]
        private Transaction? transaction;

        [ObservableProperty]
        private DateTime selectedDate = DateTime.Now;

        [ObservableProperty]
        private string description = string.Empty;

        [ObservableProperty]
        private string amount = string.Empty;

        [ObservableProperty]
        private Category? selectedCategory;

        [ObservableProperty]
        private List<Category> categories = new();

        [ObservableProperty]
        private Account? selectedAccount;

        [ObservableProperty]
        private List<Account> accounts = new();

        [ObservableProperty]
        private string title = "Add Transaction";

        [ObservableProperty]
        private bool isEditMode = false;

        public AddTransactionViewModel(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        /// <summary>
        /// Resets the ViewModel state for a fresh start
        /// </summary>
        private void ResetState()
        {
            Transaction = null;
            TransactionType = TransactionType.Expense;
            SelectedDate = DateTime.Now;
            Description = string.Empty;
            Amount = string.Empty;
            SelectedCategory = null;
            SelectedAccount = null;
            Title = "Add Transaction";
            IsEditMode = false;
        }

        public async Task InitializeAsync()
        {
            // Save navigation parameters before reset
            var savedTransaction = Transaction;
            var savedType = TransactionType;
            
            // Reset state to handle ViewModel reuse
            ResetState();
            
            // Restore navigation parameters
            Transaction = savedTransaction;
            TransactionType = savedType;

            await LoadAccountsAsync();
            await LoadCategoriesAsync();
            
            if (Transaction != null)
            {
                // Edit mode
                IsEditMode = true;
                TransactionType = Transaction.Type;
                SelectedDate = Transaction.Date;
                Description = Transaction.Description;
                Amount = Transaction.Amount.ToString();
                
                Title = TransactionType == TransactionType.Income ? "Edit Income" : "Edit Expense";
                
                // Load categories and select the current one
                await LoadCategoriesAsync();
                SelectedCategory = Categories.FirstOrDefault(c => c.Id == Transaction.CategoryId);
                
                // Select the current account
                if (Transaction.AccountId.HasValue)
                {
                    SelectedAccount = Accounts.FirstOrDefault(a => a.Id == Transaction.AccountId.Value);
                }
                else
                {
                    SelectedAccount = Accounts.FirstOrDefault(a => a.IsDefault);
                }
            }
            else
            {
                // Add mode
                IsEditMode = false;
                Title = TransactionType == TransactionType.Income ? "Add Income" : "Add Expense";
                
                // Default to Cash account
                SelectedAccount = Accounts.FirstOrDefault(a => a.IsDefault) ?? Accounts.FirstOrDefault();
            }
        }

        private async Task LoadAccountsAsync()
        {
            Accounts = await _databaseService.GetAccountsAsync();
        }

        private async Task LoadCategoriesAsync()
        {
            var categoryType = TransactionType == TransactionType.Income ? CategoryType.Income : CategoryType.Expense;
            Categories = await _databaseService.GetCategoriesAsync(categoryType);
            
            if (!IsEditMode)
            {
                SelectedCategory = Categories.FirstOrDefault();
            }
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            if (string.IsNullOrWhiteSpace(Description))
            {
                await Shell.Current.DisplayAlertAsync("Error", "Please enter a description", "OK");
                return;
            }

            var amountValue = CurrencyMaskBehavior.ParseCurrencyValue(Amount);
            if (amountValue <= 0)
            {
                await Shell.Current.DisplayAlertAsync("Error", "Please enter a valid amount", "OK");
                return;
            }

            if (SelectedAccount == null)
            {
                await Shell.Current.DisplayAlertAsync("Error", "Please select an account", "OK");
                return;
            }

            if (SelectedCategory == null)
            {
                await Shell.Current.DisplayAlertAsync("Error", "Please select a category", "OK");
                return;
            }

            int accountId = SelectedAccount.Id;

            if (IsEditMode && Transaction != null)
            {
                // Update existing transaction
                Transaction.Date = SelectedDate;
                Transaction.Description = Description;
                Transaction.Amount = amountValue;
                Transaction.CategoryId = SelectedCategory.Id;
                Transaction.Type = TransactionType;
                Transaction.AccountId = accountId;
                
                await _databaseService.SaveTransactionAsync(Transaction);
                await Shell.Current.DisplayAlertAsync("Success", "Transaction updated successfully", "OK");
            }
            else
            {
                // Create new transaction
                var newTransaction = new Transaction
                {
                    Date = SelectedDate,
                    Description = Description,
                    Amount = amountValue,
                    CategoryId = SelectedCategory.Id,
                    Type = TransactionType,
                    AccountId = accountId
                };

                await _databaseService.SaveTransactionAsync(newTransaction);
            }

            await Shell.Current.GoToAsync("..");
        }

        [RelayCommand]
        private async Task CancelAsync()
        {
            await Shell.Current.GoToAsync("..");
        }
    }
}

