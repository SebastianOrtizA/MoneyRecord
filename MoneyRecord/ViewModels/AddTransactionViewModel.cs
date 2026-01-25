using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
        private string title = "Add Transaction";

        [ObservableProperty]
        private bool isEditMode = false;

        public AddTransactionViewModel(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        public async Task InitializeAsync()
        {
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
            }
            else
            {
                // Add mode
                IsEditMode = false;
                Title = TransactionType == TransactionType.Income ? "Add Income" : "Add Expense";
            }
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
            if (SelectedCategory == null)
            {
                await Shell.Current.DisplayAlert("Error", "Please select a category", "OK");
                return;
            }

            if (!decimal.TryParse(Amount, out var amountValue) || amountValue <= 0)
            {
                await Shell.Current.DisplayAlert("Error", "Please enter a valid amount", "OK");
                return;
            }

            if (IsEditMode && Transaction != null)
            {
                // Update existing transaction
                Transaction.Date = SelectedDate;
                Transaction.Description = Description;
                Transaction.Amount = amountValue;
                Transaction.CategoryId = SelectedCategory.Id;
                Transaction.Type = TransactionType;
                
                await _databaseService.SaveTransactionAsync(Transaction);
                await Shell.Current.DisplayAlert("Success", "Transaction updated successfully", "OK");
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
                    Type = TransactionType
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

