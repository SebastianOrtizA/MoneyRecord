using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyRecord.Models;
using MoneyRecord.Services;
using System.Collections.ObjectModel;

namespace MoneyRecord.ViewModels
{
    public partial class ManageAccountsViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;

        [ObservableProperty]
        private ObservableCollection<Account> accounts = new();

        [ObservableProperty]
        private string newAccountName = string.Empty;

        [ObservableProperty]
        private string newAccountBalance = "0";

        [ObservableProperty]
        private bool isEditMode = false;

        [ObservableProperty]
        private string editAccountName = string.Empty;

        [ObservableProperty]
        private string editAccountBalance = "0";

        [ObservableProperty]
        private Account? editingAccount;

        public ManageAccountsViewModel(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        public async Task InitializeAsync()
        {
            await LoadAccountsAsync();
        }

        private async Task LoadAccountsAsync()
        {
            var accountList = await _databaseService.GetAccountsAsync();
            Accounts.Clear();
            foreach (var account in accountList)
            {
                Accounts.Add(account);
            }
        }

        [RelayCommand]
        private async Task AddAccountAsync()
        {
            if (string.IsNullOrWhiteSpace(NewAccountName))
            {
                await Shell.Current.DisplayAlert("Error", "Please enter an account name", "OK");
                return;
            }

            if (!decimal.TryParse(NewAccountBalance, out var balance))
            {
                balance = 0;
            }

            var account = new Account
            {
                Name = NewAccountName.Trim(),
                InitialBalance = balance,
                IsDefault = false
            };

            await _databaseService.SaveAccountAsync(account);
            NewAccountName = string.Empty;
            NewAccountBalance = "0";
            await LoadAccountsAsync();
        }

        [RelayCommand]
        private void EditAccount(Account account)
        {
            if (account == null)
                return;

            EditingAccount = account;
            EditAccountName = account.Name;
            EditAccountBalance = account.InitialBalance.ToString();
            IsEditMode = true;
        }

        [RelayCommand]
        private void CancelEdit()
        {
            IsEditMode = false;
            EditingAccount = null;
            EditAccountName = string.Empty;
            EditAccountBalance = "0";
        }

        [RelayCommand]
        private async Task SaveEditAsync()
        {
            if (EditingAccount == null)
                return;

            if (string.IsNullOrWhiteSpace(EditAccountName))
            {
                await Shell.Current.DisplayAlert("Error", "Please enter an account name", "OK");
                return;
            }

            if (!decimal.TryParse(EditAccountBalance, out var balance))
            {
                balance = EditingAccount.InitialBalance;
            }

            EditingAccount.Name = EditAccountName.Trim();
            EditingAccount.InitialBalance = balance;

            await _databaseService.SaveAccountAsync(EditingAccount);
            
            IsEditMode = false;
            EditingAccount = null;
            EditAccountName = string.Empty;
            EditAccountBalance = "0";
            
            await LoadAccountsAsync();
        }

        [RelayCommand]
        private async Task DeleteAccountAsync(Account account)
        {
            if (account == null)
                return;

            if (account.IsDefault)
            {
                await Shell.Current.DisplayAlert("Error", "Cannot delete the default Cash account", "OK");
                return;
            }

            var hasTransactions = await _databaseService.AccountHasTransactionsAsync(account.Id);
            
            string message = hasTransactions
                ? $"Are you sure you want to delete '{account.Name}'?\n\nAll transactions in this account will be moved to the Cash account."
                : $"Are you sure you want to delete '{account.Name}'?";

            var confirm = await Shell.Current.DisplayAlert(
                "Confirm Delete",
                message,
                "Yes, Delete",
                "Cancel");

            if (!confirm)
                return;

            await _databaseService.DeleteAccountAsync(account);
            await LoadAccountsAsync();
        }

        [RelayCommand]
        private async Task GoBackAsync()
        {
            await Shell.Current.GoToAsync("..");
        }
    }
}
