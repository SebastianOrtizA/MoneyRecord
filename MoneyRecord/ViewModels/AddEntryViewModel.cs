using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyRecord.Models;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.Views;

namespace MoneyRecord.ViewModels
{
    /// <summary>The app's one add entry point: a button that opens a sheet with Expense / Income / Transfer.</summary>
    public partial class AddEntryViewModel : ObservableObject
    {
        private readonly INavigationService _navigationService;

        [ObservableProperty]
        private bool isOpen;

        public AddEntryViewModel(INavigationService navigationService)
        {
            _navigationService = navigationService;
        }

        [RelayCommand]
        private void Open() => IsOpen = true;

        [RelayCommand]
        private void Close() => IsOpen = false;

        [RelayCommand]
        private Task AddExpenseAsync() => AddTransactionAsync(TransactionType.Expense);

        /// <summary>Long-press on the button: straight to a new expense, skipping the sheet.</summary>
        [RelayCommand]
        private Task AddExpenseNowAsync() => AddTransactionAsync(TransactionType.Expense);

        [RelayCommand]
        private Task AddIncomeAsync() => AddTransactionAsync(TransactionType.Income);

        [RelayCommand]
        private Task AddTransferAsync()
        {
            IsOpen = false;
            return _navigationService.NavigateToAsync(nameof(AddTransferPage));
        }

        private Task AddTransactionAsync(TransactionType type)
        {
            IsOpen = false;
            return _navigationService.NavigateToAsync(nameof(AddTransactionPage),
                new Dictionary<string, object> { { "TransactionType", type } });
        }
    }
}
