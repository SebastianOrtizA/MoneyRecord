using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyRecord.Behaviors;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services;

namespace MoneyRecord.ViewModels
{
    [QueryProperty(nameof(TransactionType), "TransactionType")]
    [QueryProperty(nameof(Transaction), "Transaction")]
    public partial class AddTransactionViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;
        private readonly INavigationService _navigationService;

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
        private string title = string.Empty;

        [ObservableProperty]
        private bool isEditMode = false;

        public AddTransactionViewModel(DatabaseService databaseService, INavigationService navigationService)
        {
            _databaseService = databaseService;
            _navigationService = navigationService;
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
            Title = AppResources.AddTransaction;
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
                
                Title = TransactionType == TransactionType.Income ? AppResources.EditIncome : AppResources.EditExpense;
                
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
                Title = TransactionType == TransactionType.Income ? AppResources.AddIncome : AppResources.AddExpense;
                
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
                await Shell.Current.DisplayAlertAsync(AppResources.Error, AppResources.PleaseEnterDescription, AppResources.OK);
                return;
            }

            var amountValue = CurrencyMaskBehavior.ParseCurrencyValue(Amount);
            if (amountValue <= 0)
            {
                await Shell.Current.DisplayAlertAsync(AppResources.Error, AppResources.PleaseEnterValidAmount, AppResources.OK);
                return;
            }

            if (SelectedAccount == null)
            {
                await Shell.Current.DisplayAlertAsync(AppResources.Error, AppResources.PleaseSelectAccount, AppResources.OK);
                return;
            }

            if (SelectedCategory == null)
            {
                await Shell.Current.DisplayAlertAsync(AppResources.Error, AppResources.PleaseSelectCategory, AppResources.OK);
                return;
            }


            int accountId = SelectedAccount.Id;

            // Validate balance for expense transactions if account doesn't allow negative balance
            if (TransactionType == TransactionType.Expense && !SelectedAccount.AllowNegativeBalance)
            {
                var currentBalance = await _databaseService.GetAccountBalanceAsync(accountId);
                
                // For edit mode, add back the original expense amount before checking
                decimal adjustedBalance = currentBalance;
                if (IsEditMode && Transaction != null && Transaction.Type == TransactionType.Expense)
                {
                    adjustedBalance += Transaction.Amount;
                }
                
                if (adjustedBalance - amountValue < 0)
                {
                    var message = string.Format(AppResources.InsufficientAccountBalance, SelectedAccount.Name);
                    await Shell.Current.DisplayAlertAsync(AppResources.Error, message, AppResources.OK);
                    return;
                }
            }

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
                await Shell.Current.DisplayAlertAsync(AppResources.Success, AppResources.TransactionUpdatedSuccessfully, AppResources.OK);
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

            await _navigationService.GoBackAsync();
        }

        [RelayCommand]
        private async Task CancelAsync()
        {
            await _navigationService.GoBackAsync();
        }
    }
}

