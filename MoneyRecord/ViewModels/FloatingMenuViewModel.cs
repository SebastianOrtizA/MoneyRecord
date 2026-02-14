using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyRecord.Models;
using MoneyRecord.Views;

namespace MoneyRecord.ViewModels
{
    public partial class FloatingMenuViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool isExpanded;

        [RelayCommand]
        private void ToggleMenu()
        {
            IsExpanded = !IsExpanded;
        }

        [RelayCommand]
        private async Task AddIncome()
        {
            IsExpanded = false;
            await Shell.Current.GoToAsync(nameof(AddTransactionPage), new Dictionary<string, object>
            {
                { "TransactionType", TransactionType.Income }
            });
        }

        [RelayCommand]
        private async Task AddExpense()
        {
            IsExpanded = false;
            await Shell.Current.GoToAsync(nameof(AddTransactionPage), new Dictionary<string, object>
            {
                { "TransactionType", TransactionType.Expense }
            });
        }

        [RelayCommand]
        private async Task AddTransfer()
        {
            IsExpanded = false;
            await Shell.Current.GoToAsync(nameof(AddTransferPage));
        }

        [RelayCommand]
        private void CloseMenu()
        {
            IsExpanded = false;
        }
    }
}
