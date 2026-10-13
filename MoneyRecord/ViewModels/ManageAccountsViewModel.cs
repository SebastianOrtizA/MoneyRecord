using MoneyRecord.Helpers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;
using System.Collections.ObjectModel;

namespace MoneyRecord.ViewModels
{
    [QueryProperty(nameof(CurrencyFilter), "CurrencyCode")]
    public partial class ManageAccountsViewModel : ObservableObject
    {
        private readonly IAccountRepository _accountRepository;
        private readonly IAccountService _accountService;
        private readonly ICategoryIconService _categoryIconService;
        private readonly IBalanceService _balanceService;
        private readonly ICurrencyService _currencyService;
        private readonly IErrorHandler _errorHandler;
        private readonly IDialogService _dialogService;

        /// <summary>The accounts grouped by currency, base currency first, each with its subtotal.</summary>
        [ObservableProperty]
        private List<AccountGroup> groups = new();

        /// <summary>From a Home currency line: only the accounts in this currency are listed (null = all).</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasCurrencyFilter), nameof(CurrencyFilterText))]
        private string? currencyFilter;

        public bool HasCurrencyFilter => !string.IsNullOrEmpty(CurrencyFilter);

        public string CurrencyFilterText => string.Format(AppResources.ShowingCurrencyAccounts, CurrencyFilter);

        /// <summary>The add/edit sheet is open.</summary>
        [ObservableProperty]
        private bool isFormOpen;

        /// <summary>The account the sheet edits; null when it adds a new one.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsEditMode), nameof(FormTitle), nameof(SaveText), nameof(CanDelete))]
        private Account? editingAccount;

        public bool IsEditMode => EditingAccount != null;

        public string FormTitle => IsEditMode ? AppResources.EditAccount : AppResources.AddNewAccount;

        public string SaveText => IsEditMode ? AppResources.Save : AppResources.AddAccountButton;

        /// <summary>The default account can't be deleted, so the sheet hides Delete for it.</summary>
        public bool CanDelete => EditingAccount is { IsDefault: false };

        [ObservableProperty]
        private string formName = string.Empty;

        [ObservableProperty]
        private string formBalance = "0";

        [ObservableProperty]
        private string formIconCode = string.Empty;

        [ObservableProperty]
        private bool formAllowNegativeBalance;

        [ObservableProperty]
        private Currency? formCurrency;

        [ObservableProperty]
        private ObservableCollection<AccountIcon> availableIcons = new();

        [ObservableProperty]
        private List<Currency> availableCurrencies = new();

        public ManageAccountsViewModel(IAccountRepository accountRepository, IAccountService accountService, ICategoryIconService categoryIconService, IBalanceService balanceService, ICurrencyService currencyService, IErrorHandler errorHandler, IDialogService dialogService)
        {
            _accountRepository = accountRepository;
            _accountService = accountService;
            _categoryIconService = categoryIconService;
            _balanceService = balanceService;
            _currencyService = currencyService;
            _errorHandler = errorHandler;
            _dialogService = dialogService;
        }

        public Task InitializeAsync()
        {
            if (AvailableIcons.Count == 0)
                AvailableIcons = new ObservableCollection<AccountIcon>(_categoryIconService.GetAccountIcons());
            if (AvailableCurrencies.Count == 0)
                AvailableCurrencies = _currencyService.GetAvailableCurrencies();
            return LoadAccountsCommand.ExecuteAsync(null);
        }

        [RelayCommand(AllowConcurrentExecutions = true)]
        private async Task LoadAccountsAsync(CancellationToken cancellationToken)
        {
            try
            {
                var accountsTask = _accountRepository.GetAllAsync();
                var balancesTask = _balanceService.GetAllAccountBalancesAsync();
                await Task.WhenAll(accountsTask, balancesTask);
                cancellationToken.ThrowIfCancellationRequested();

                Groups = AccountGroup.Build(await accountsTask, await balancesTask,
                    _currencyService.GetBaseCurrencyCode(), CurrencyFilter);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // A newer load replaced this one
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToLoadAccountBalances);
            }
        }

        [RelayCommand]
        private void ClearCurrencyFilter()
        {
            CurrencyFilter = null;
            LoadAccountsCommand.Execute(null);
        }

        /// <summary>
        /// Opens the sheet for a new account. Its currency is the filter's when the list is filtered (a new account would
        /// otherwise vanish from the list right after saving), else the base currency.
        /// </summary>
        [RelayCommand]
        private void AddAccount()
        {
            var code = HasCurrencyFilter ? CurrencyFilter : _currencyService.GetDefaultCurrencyCode();
            EditingAccount = null;
            FormName = string.Empty;
            FormAllowNegativeBalance = false;
            FormCurrency = AvailableCurrencies.FirstOrDefault(c => c.Code == code) ?? AvailableCurrencies.FirstOrDefault();
            FormBalance = "0";
            SelectIcon(_categoryIconService.GetDefaultAccountIconCode());
            IsFormOpen = true;
        }

        [RelayCommand]
        private void EditAccount(AccountListItem? item)
        {
            if (item == null)
                return;

            var account = item.Account;
            EditingAccount = account;
            FormName = account.Name;
            FormAllowNegativeBalance = account.AllowNegativeBalance;
            FormCurrency = AvailableCurrencies.FirstOrDefault(c => c.Code == item.CurrencyCode);
            // After the currency: the balance field formats in it
            FormBalance = account.InitialBalance.ToString();
            SelectIcon(account.IconCode);
            IsFormOpen = true;
        }

        [RelayCommand]
        private void CloseForm()
        {
            IsFormOpen = false;
            EditingAccount = null;
        }

        /// <summary>Closes the add/edit sheet; false when it wasn't open.</summary>
        public bool CloseTopOverlay()
        {
            if (!IsFormOpen)
                return false;

            CloseForm();
            return true;
        }

        [RelayCommand]
        private void SelectIcon(string iconCode)
        {
            FormIconCode = iconCode;
            foreach (var icon in AvailableIcons)
            {
                icon.IsSelected = icon.Code == iconCode;
            }
        }

        [RelayCommand]
        private async Task SaveAccountAsync()
        {
            if (string.IsNullOrWhiteSpace(FormName))
            {
                await _errorHandler.HandleAsync(AppResources.PleaseEnterAccountName);
                return;
            }

            // Always parse with allowNegative=true to detect negative values for validation
            var balance = AmountParser.Parse(FormBalance, FormCurrency?.Code, allowNegative: true);

            if (!Money.IsInRange(balance))
            {
                await _errorHandler.HandleAsync(AppResources.PleaseEnterValidAmount);
                return;
            }

            // Validate: negative initial balance only allowed if AllowNegativeBalance is enabled
            if (balance < 0 && !FormAllowNegativeBalance)
            {
                await _errorHandler.HandleAsync(AppResources.NegativeBalanceNotAllowed);
                return;
            }

            var account = EditingAccount ?? new Account { IsDefault = false, CreatedDate = DateTime.Now };
            account.Name = FormName.Trim();
            account.InitialBalance = balance;
            account.IconCode = string.IsNullOrEmpty(FormIconCode)
                ? _categoryIconService.GetDefaultAccountIconCode()
                : FormIconCode;
            account.AllowNegativeBalance = FormAllowNegativeBalance;
            account.CurrencyCode = FormCurrency?.Code ?? _currencyService.GetDefaultCurrencyCode();

            await _accountRepository.SaveAsync(account);
            CloseForm();
            await LoadAccountsCommand.ExecuteAsync(null);
        }

        /// <summary>Deletes the account in the sheet; the sheet stays open behind the question, so cancelling returns to it.</summary>
        [RelayCommand]
        private async Task DeleteAccountAsync()
        {
            var account = EditingAccount;
            if (account == null)
                return;

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

                var confirm = await _dialogService.ConfirmAsync(
                    AppResources.ConfirmDelete,
                    message,
                    AppResources.YesDelete,
                    AppResources.Cancel);

                if (!confirm)
                    return;

                await _accountService.DeleteAsync(account.Id);
                CloseForm();
                await LoadAccountsCommand.ExecuteAsync(null);
            }
            catch (InvalidOperationException ex)
            {
                // A service guard fired because the data changed after the usage check (e.g. a transfer was added)
                await _errorHandler.HandleAsync(ex, string.Format(AppResources.DeleteFailedDataChanged, account.Name));
                CloseForm();
                await LoadAccountsCommand.ExecuteAsync(null);
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToDeleteAccount);
            }
        }
    }
}
