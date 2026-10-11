using MoneyRecord.Helpers;
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
    public sealed class AccountDisplayItem
    {
        public Account Account { get; init; } = null!;
        public decimal CurrentBalance { get; init; }
    }

    public partial class ManageAccountsViewModel : ObservableObject
    {
        private readonly IAccountRepository _accountRepository;
        private readonly IAccountService _accountService;
        private readonly ICategoryIconService _categoryIconService;
        private readonly IBalanceService _balanceService;
        private readonly ICurrencyService _currencyService;
        private readonly IErrorHandler _errorHandler;

        [ObservableProperty]
        private ObservableCollection<AccountDisplayItem> accounts = new();

        [ObservableProperty]
        private string newAccountName = string.Empty;

        [ObservableProperty]
        private string newAccountBalance = "0";

        [ObservableProperty]
        private string newAccountIconCode = string.Empty;

        [ObservableProperty]
        private bool newAccountAllowNegativeBalance = false;

        [ObservableProperty]
        private Currency? newAccountCurrency;

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
        private Currency? editAccountCurrency;

        [ObservableProperty]
        private Account? editingAccount;

        [ObservableProperty]
        private ObservableCollection<AccountIcon> availableIcons = new();

        [ObservableProperty]
        private List<Currency> availableCurrencies = new();

        public ManageAccountsViewModel(IAccountRepository accountRepository, IAccountService accountService, ICategoryIconService categoryIconService, IBalanceService balanceService, ICurrencyService currencyService, IErrorHandler errorHandler)
        {
            _accountRepository = accountRepository;
            _accountService = accountService;
            _categoryIconService = categoryIconService;
            _balanceService = balanceService;
            _currencyService = currencyService;
            _errorHandler = errorHandler;
        }

        public async Task InitializeAsync()
        {
            LoadAvailableIcons();
            AvailableCurrencies = _currencyService.GetAvailableCurrencies();
            NewAccountIconCode = _categoryIconService.GetDefaultAccountIconCode();
            UpdateIconSelection(NewAccountIconCode);
            var defaultCode = _currencyService.GetDefaultCurrencyCode();
            NewAccountCurrency = AvailableCurrencies.FirstOrDefault(c => c.Code == defaultCode) ?? AvailableCurrencies.First();
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
            var accountList = await _accountRepository.GetAllAsync();
            var balances = await _balanceService.GetAllAccountBalancesAsync();
            var balanceMap = balances.ToDictionary(b => b.AccountId, b => b.CurrentBalance);

            Accounts.Clear();
            foreach (var account in accountList)
            {
                Accounts.Add(new AccountDisplayItem
                {
                    Account = account,
                    CurrentBalance = balanceMap.GetValueOrDefault(account.Id)
                });
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
                await _errorHandler.HandleAsync(AppResources.PleaseEnterAccountName);
                return;
            }

            // Always parse with allowNegative=true to detect negative values for validation
            var balance = CurrencyMaskBehavior.ParseAmount(NewAccountBalance, NewAccountCurrency?.Code, allowNegative: true);

            if (!Money.IsInRange(balance))
            {
                await _errorHandler.HandleAsync(AppResources.PleaseEnterValidAmount);
                return;
            }

            // Validate: negative initial balance only allowed if AllowNegativeBalance is enabled
            if (balance < 0 && !NewAccountAllowNegativeBalance)
            {
                await _errorHandler.HandleAsync(AppResources.NegativeBalanceNotAllowed);
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
                AllowNegativeBalance = NewAccountAllowNegativeBalance,
                CurrencyCode = NewAccountCurrency?.Code ?? _currencyService.GetDefaultCurrencyCode()
            };

            await _accountRepository.SaveAsync(account);
            NewAccountName = string.Empty;
            NewAccountBalance = "0";
            NewAccountIconCode = _categoryIconService.GetDefaultAccountIconCode();
            NewAccountAllowNegativeBalance = false;
            NewAccountCurrency = AvailableCurrencies.FirstOrDefault(c => c.Code == _currencyService.GetDefaultCurrencyCode());
            UpdateIconSelection(NewAccountIconCode);
            await LoadAccountsAsync();
        }

        [RelayCommand]
        private void EditAccount(AccountDisplayItem item)
        {
            if (item?.Account == null)
                return;

            var account = item.Account;
            EditingAccount = account;
            EditAccountName = account.Name;
            EditAccountBalance = account.InitialBalance.ToString();
            EditAccountIconCode = account.IconCode;
            EditAccountAllowNegativeBalance = account.AllowNegativeBalance;
            EditAccountCurrency = AvailableCurrencies.FirstOrDefault(c => c.Code == account.CurrencyCode)
                ?? AvailableCurrencies.FirstOrDefault(c => c.Code == _currencyService.GetDefaultCurrencyCode());
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
                await _errorHandler.HandleAsync(AppResources.PleaseEnterAccountName);
                return;
            }

            // Always parse with allowNegative=true to detect negative values for validation
            var balance = CurrencyMaskBehavior.ParseAmount(EditAccountBalance, EditAccountCurrency?.Code, allowNegative: true);

            if (!Money.IsInRange(balance))
            {
                await _errorHandler.HandleAsync(AppResources.PleaseEnterValidAmount);
                return;
            }

            // Validate: negative initial balance only allowed if AllowNegativeBalance is enabled
            if (balance < 0 && !EditAccountAllowNegativeBalance)
            {
                await _errorHandler.HandleAsync(AppResources.NegativeBalanceNotAllowed);
                return;
            }

            EditingAccount.Name = EditAccountName.Trim();
            EditingAccount.InitialBalance = balance;
            EditingAccount.IconCode = string.IsNullOrEmpty(EditAccountIconCode)
                ? _categoryIconService.GetDefaultAccountIconCode()
                : EditAccountIconCode;
            EditingAccount.AllowNegativeBalance = EditAccountAllowNegativeBalance;
            EditingAccount.CurrencyCode = EditAccountCurrency?.Code ?? _currencyService.GetDefaultCurrencyCode();

            await _accountRepository.SaveAsync(EditingAccount);
            
            IsEditMode = false;
            EditingAccount = null;
            EditAccountName = string.Empty;
            EditAccountBalance = "0";
            EditAccountIconCode = string.Empty;
            EditAccountAllowNegativeBalance = false;
            EditAccountCurrency = null;
            UpdateIconSelection(NewAccountIconCode);

            await LoadAccountsAsync();
        }

        [RelayCommand]
        private async Task DeleteAccountAsync(AccountDisplayItem item)
        {
            if (item?.Account == null)
                return;

            var account = item.Account;

            if (account.IsDefault)
            {
                await _errorHandler.HandleAsync(AppResources.CannotDeleteDefaultAccount);
                return;
            }

            try
            {
                var usage = await _accountService.GetUsageAsync(account.Id);

                // Moving transfers to the default account could create a transfer from an account to itself
                if (usage.Transfers > 0)
                {
                    await _errorHandler.HandleAsync(string.Format(AppResources.AccountHasTransfers, account.Name, usage.Transfers));
                    return;
                }

                string message;
                if (usage.Transactions > 0 || usage.RecurringRules > 0)
                {
                    var defaultAccount = await _accountRepository.GetDefaultAsync();
                    if (defaultAccount != null && !AccountService.HaveSameCurrency(account, defaultAccount))
                    {
                        await _errorHandler.HandleAsync(string.Format(AppResources.AccountCurrencyDiffersFromDefault,
                            account.Name, MoneyFormatter.ResolveCurrencyCode(account.CurrencyCode),
                            defaultAccount.Name, MoneyFormatter.ResolveCurrencyCode(defaultAccount.CurrencyCode)));
                        return;
                    }

                    message = string.Format(AppResources.DeleteAccountWithTransactions,
                        account.Name, usage.Transactions, usage.RecurringRules, defaultAccount?.Name);
                }
                else
                {
                    message = string.Format(AppResources.DeleteAccountNoTransactions, account.Name);
                }

                var confirm = await Shell.Current.DisplayAlertAsync(
                    AppResources.ConfirmDelete,
                    message,
                    AppResources.YesDelete,
                    AppResources.Cancel);

                if (!confirm)
                    return;

                await _accountService.DeleteAsync(account.Id);
                await LoadAccountsAsync();
            }
            catch (InvalidOperationException ex)
            {
                // A service guard fired because the data changed after the usage check (e.g. a transfer was added)
                await _errorHandler.HandleAsync(ex, string.Format(AppResources.DeleteFailedDataChanged, account.Name));
                await LoadAccountsAsync();
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, ex.Message);
            }
        }

        [RelayCommand]
        private async Task GoBackAsync()
        {
            await Shell.Current.GoToAsync("..");
        }
    }
}
