using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyRecord.Models;
using MoneyRecord.Services;
using MoneyRecord.Views;

namespace MoneyRecord.ViewModels
{
    public partial class FloatingMenuViewModel : ObservableObject
    {
        private readonly INavigationService _navigationService;

        [ObservableProperty]
        private bool isExpanded;

        public FloatingMenuViewModel(INavigationService navigationService)
        {
            _navigationService = navigationService;
        }

        [RelayCommand]
        private void ToggleMenu()
        {
            IsExpanded = !IsExpanded;
        }

        [RelayCommand]
        private async Task AddIncome()
        {
            IsExpanded = false;
            await _navigationService.NavigateToAsync(nameof(AddTransactionPage), new Dictionary<string, object>
            {
                { "TransactionType", TransactionType.Income }
            });
        }

        [RelayCommand]
        private async Task AddExpense()
        {
            IsExpanded = false;
            await _navigationService.NavigateToAsync(nameof(AddTransactionPage), new Dictionary<string, object>
            {
                { "TransactionType", TransactionType.Expense }
            });
        }

        [RelayCommand]
        private async Task AddTransfer()
        {
            IsExpanded = false;
            await _navigationService.NavigateToAsync(nameof(AddTransferPage));
        }

        [RelayCommand]
        private void CloseMenu()
        {
            IsExpanded = false;
        }
    }
}
