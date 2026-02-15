using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyRecord.Behaviors;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;
using System.Collections.ObjectModel;

namespace MoneyRecord.ViewModels
{
    public partial class ManageAccountsViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;
        private readonly ICategoryIconService _categoryIconService;

        [ObservableProperty]
        private ObservableCollection<Account> accounts = new();

        [ObservableProperty]
        private string newAccountName = string.Empty;

        [ObservableProperty]
        private string newAccountBalance = "0";

        [ObservableProperty]
        private string newAccountIconCode = string.Empty;

        [ObservableProperty]
        private bool newAccountAllowNegativeBalance = false;

        [ObservableProperty]
        private bool isEditMode = false;

        [ObservableProperty]
        private string editAccountName = string.Empty;

        [ObservableProperty]
        private string editAccountBalance = "0";

        [ObservableProperty]
        private string editAccountIconCode = string.Empty;

        [ObservableProperty]
        private bool editAccountAllowNegativeBalance = false;

        [ObservableProperty]
        private Account? editingAccount;

        [ObservableProperty]
        private ObservableCollection<AccountIcon> availableIcons = new();

        public ManageAccountsViewModel(DatabaseService databaseService, ICategoryIconService categoryIconService)
        {
            _databaseService = databaseService;
            _categoryIconService = categoryIconService;
        }

        public async Task InitializeAsync()
        {
            LoadAvailableIcons();
            NewAccountIconCode = _categoryIconService.GetDefaultAccountIconCode();
            UpdateIconSelection(NewAccountIconCode);
            await LoadAccountsAsync();
        }

        private void LoadAvailableIcons()
        {
            AvailableIcons.Clear();
            var icons = _categoryIconService.GetAccountIcons();
            foreach (var icon in icons)
            {
                AvailableIcons.Add(icon);
            }
        }

        private void UpdateIconSelection(string selectedCode)
        {
            foreach (var icon in AvailableIcons)
            {
                icon.IsSelected = icon.Code == selectedCode;
            }
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
        private void SelectNewIcon(string iconCode)
        {
            NewAccountIconCode = iconCode;
            UpdateIconSelection(iconCode);
        }

        [RelayCommand]
        private void SelectEditIcon(string iconCode)
        {
            EditAccountIconCode = iconCode;
            UpdateIconSelection(iconCode);
        }

        [RelayCommand]
        private async Task AddAccountAsync()
        {
            if (string.IsNullOrWhiteSpace(NewAccountName))
            {
                await Shell.Current.DisplayAlertAsync(AppResources.Error, AppResources.PleaseEnterAccountName, AppResources.OK);
                return;
            }

            // Always parse with allowNegative=true to detect negative values for validation
            var balance = CurrencyMaskBehavior.ParseCurrencyValue(NewAccountBalance, 2, allowNegative: true);

            // Validate: negative initial balance only allowed if AllowNegativeBalance is enabled
            if (balance < 0 && !NewAccountAllowNegativeBalance)
            {
                await Shell.Current.DisplayAlertAsync(AppResources.Error, AppResources.NegativeBalanceNotAllowed, AppResources.OK);
                return;
            }

            var account = new Account
            {
                Name = NewAccountName.Trim(),
                InitialBalance = balance,
                IsDefault = false,
                IconCode = string.IsNullOrEmpty(NewAccountIconCode) 
                    ? _categoryIconService.GetDefaultAccountIconCode() 
                    : NewAccountIconCode,
                CreatedDate = DateTime.Now,
                AllowNegativeBalance = NewAccountAllowNegativeBalance
            };

            await _databaseService.SaveAccountAsync(account);
            NewAccountName = string.Empty;
            NewAccountBalance = "0";
            NewAccountIconCode = _categoryIconService.GetDefaultAccountIconCode();
            NewAccountAllowNegativeBalance = false;
            UpdateIconSelection(NewAccountIconCode);
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
            EditAccountIconCode = account.IconCode;
            EditAccountAllowNegativeBalance = account.AllowNegativeBalance;
            UpdateIconSelection(account.IconCode);
            IsEditMode = true;
        }

        [RelayCommand]
        private void CancelEdit()
        {
            IsEditMode = false;
            EditingAccount = null;
            EditAccountName = string.Empty;
            EditAccountBalance = "0";
            EditAccountIconCode = string.Empty;
            EditAccountAllowNegativeBalance = false;
            UpdateIconSelection(NewAccountIconCode);
        }

        [RelayCommand]
        private async Task SaveEditAsync()
        {
            if (EditingAccount == null)
                return;

            if (string.IsNullOrWhiteSpace(EditAccountName))
            {
                await Shell.Current.DisplayAlertAsync("Error", "Please enter an account name", "OK");
                return;
            }

            // Always parse with allowNegative=true to detect negative values for validation
            var balance = CurrencyMaskBehavior.ParseCurrencyValue(EditAccountBalance, 2, allowNegative: true);

            // Validate: negative initial balance only allowed if AllowNegativeBalance is enabled
            if (balance < 0 && !EditAccountAllowNegativeBalance)
            {
                await Shell.Current.DisplayAlertAsync(AppResources.Error, AppResources.NegativeBalanceNotAllowed, AppResources.OK);
                return;
            }

            EditingAccount.Name = EditAccountName.Trim();
            EditingAccount.InitialBalance = balance;
            EditingAccount.IconCode = string.IsNullOrEmpty(EditAccountIconCode)
                ? _categoryIconService.GetDefaultAccountIconCode()
                : EditAccountIconCode;
            EditingAccount.AllowNegativeBalance = EditAccountAllowNegativeBalance;

            await _databaseService.SaveAccountAsync(EditingAccount);
            
            IsEditMode = false;
            EditingAccount = null;
            EditAccountName = string.Empty;
            EditAccountBalance = "0";
            EditAccountIconCode = string.Empty;
            EditAccountAllowNegativeBalance = false;
            UpdateIconSelection(NewAccountIconCode);
            
            await LoadAccountsAsync();
        }

        [RelayCommand]
        private async Task DeleteAccountAsync(Account account)
        {
            if (account == null)
                return;

            if (account.IsDefault)
            {
                await Shell.Current.DisplayAlertAsync("Error", "Cannot delete the default Cash account", "OK");
                return;
            }

            var hasTransactions = await _databaseService.AccountHasTransactionsAsync(account.Id);
            
            string message = hasTransactions
                ? $"Are you sure you want to delete '{account.Name}'?\n\nAll transactions in this account will be moved to the Cash account."
                : $"Are you sure you want to delete '{account.Name}'?";

            var confirm = await Shell.Current.DisplayAlertAsync(
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
