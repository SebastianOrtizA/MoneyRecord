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
        private readonly IExchangeRateService _exchangeRates;
        private readonly IPocketService _pocketService;

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
        [NotifyPropertyChangedFor(nameof(IsSheetOpen))]
        private bool isFormOpen;

        /// <summary>The add/edit sheet or the pocket sheet is open (the add button hides).</summary>
        public bool IsSheetOpen => IsFormOpen || IsPocketOpen;

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

        public ManageAccountsViewModel(IAccountRepository accountRepository, IAccountService accountService, ICategoryIconService categoryIconService, IBalanceService balanceService, ICurrencyService currencyService, IErrorHandler errorHandler, IDialogService dialogService, IExchangeRateService exchangeRates, IPocketService pocketService)
        {
            _pocketService = pocketService;
            _accountRepository = accountRepository;
            _accountService = accountService;
            _categoryIconService = categoryIconService;
            _balanceService = balanceService;
            _currencyService = currencyService;
            _errorHandler = errorHandler;
            _dialogService = dialogService;
            _exchangeRates = exchangeRates;
        }

        public Task InitializeAsync()
        {
            if (AvailableIcons.Count == 0)
                AvailableIcons = new ObservableCollection<AccountIcon>(_categoryIconService.GetAccountIcons());
            if (PocketIcons.Count == 0)
                PocketIcons = new ObservableCollection<AccountIcon>(_categoryIconService.GetAccountIcons());
            if (AvailableCurrencies.Count == 0)
                AvailableCurrencies = _currencyService.GetAvailableCurrencies();
            return LoadAccountsCommand.ExecuteAsync(null);
        }

        [RelayCommand(AllowConcurrentExecutions = true)]
        private async Task LoadAccountsAsync(CancellationToken cancellationToken)
        {
            try
            {
                var accountsTask = _accountRepository.GetOwnAsync();
                var balancesTask = _balanceService.GetAllAccountBalancesAsync();
                var pocketsTask = _pocketService.GetPocketsAsync();
                await Task.WhenAll(accountsTask, balancesTask, pocketsTask);
                cancellationToken.ThrowIfCancellationRequested();

                Groups = AccountGroup.Build(await accountsTask, await balancesTask,
                    _currencyService.GetBaseCurrencyCode(), CurrencyFilter, await pocketsTask);
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

        /// <summary>Closes the pocket sheet, else the add/edit sheet; false when neither was open.</summary>
        public bool CloseTopOverlay()
        {
            if (IsPocketOpen)
                ClosePocket();
            else if (IsFormOpen)
                CloseForm();
            else
                return false;
            return true;
        }

        /// <summary>The page went away: back here later shows the list, not a stale sheet.</summary>
        public void CloseAll()
        {
            ClosePocket();
            CloseForm();
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

            // Its pockets keep their amounts in the new currency but stop counting towards goals in the old one
            if (EditingAccount is { } edited && FormCurrency is { } chosen
                && !MoneyFormatter.SameCurrency(edited.CurrencyCode, chosen.Code)
                && Groups.SelectMany(g => g).FirstOrDefault(i => i.Account.Id == edited.Id) is { HasPockets: true }
                && !await _dialogService.ConfirmAsync(AppResources.Pockets,
                    string.Format(AppResources.PocketsCurrencyChange, edited.Name, MoneyFormatter.ResolveCurrencyCode(edited.CurrencyCode)),
                    AppResources.Save, AppResources.Cancel))
                return;

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
            // A new currency gets its exchange rates now, in the background (nothing is fetched for the base currency)
            _ = _exchangeRates.UpdateIfEnabledAsync();
        }

        /// <summary>Deletes the account in the sheet; the sheet stays open behind the question, so cancelling returns to it.</summary>
        [RelayCommand]
        private async Task DeleteAccountAsync()
        {
            var account = EditingAccount;
            if (account == null)
                return;

            // The sheet hides Delete for the default account; this guards the command itself
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

                if (Groups.SelectMany(g => g).FirstOrDefault(i => i.Account.Id == account.Id) is { HasPockets: true })
                    message += " " + AppResources.DeleteAccountPockets;

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

        // ----- Pocket sheet -----

        // The pocket sheet's account (set for a new pocket too)
        private Account? _pocketAccount;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsSheetOpen))]
        private bool isPocketOpen;

        /// <summary>The pocket the sheet edits; null when it adds a new one.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsPocketEdit), nameof(PocketTitle), nameof(PocketSaveText))]
        private Pocket? editingPocket;

        public bool IsPocketEdit => EditingPocket != null;

        public string PocketTitle => IsPocketEdit ? AppResources.EditPocket : AppResources.NewPocket;

        public string PocketSaveText => IsPocketEdit ? AppResources.Save : AppResources.NewPocket;

        [ObservableProperty]
        private string pocketName = string.Empty;

        [ObservableProperty]
        private string pocketIconCode = string.Empty;

        [ObservableProperty]
        private ObservableCollection<AccountIcon> pocketIcons = new();

        /// <summary>The amount to set aside or take out.</summary>
        [ObservableProperty]
        private string pocketAmount = string.Empty;

        /// <summary>The pocket's account currency: the amount field formats in it.</summary>
        [ObservableProperty]
        private string pocketCurrencyCode = string.Empty;

        /// <summary>"In this pocket: $500.00".</summary>
        [ObservableProperty]
        private string pocketHoldsText = string.Empty;

        /// <summary>"Available in Bank: $1,500.00".</summary>
        [ObservableProperty]
        private string pocketAvailableText = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasPocketMovements))]
        private List<PocketMovementRow> pocketMovements = [];

        public bool HasPocketMovements => PocketMovements.Count > 0;

        /// <summary>The account sheet's "New pocket": closes that sheet and opens the pocket sheet for the account.</summary>
        [RelayCommand]
        private async Task NewPocketAsync()
        {
            var account = EditingAccount;
            if (account == null)
                return;

            CloseForm();
            await OpenPocketSheetAsync(account, null);
        }

        [RelayCommand]
        private Task EditPocketAsync(PocketSummary? pocket) =>
            pocket == null ? Task.CompletedTask : OpenPocketSheetAsync(pocket.Account, pocket.Pocket);

        private async Task OpenPocketSheetAsync(Account account, Pocket? pocket)
        {
            _pocketAccount = account;
            EditingPocket = pocket;
            PocketName = pocket?.Name ?? string.Empty;
            PocketAmount = string.Empty;
            PocketCurrencyCode = MoneyFormatter.ResolveCurrencyCode(account.CurrencyCode);
            SelectPocketIcon(pocket?.IconCode ?? new Pocket().IconCode);
            await RefreshPocketAsync();
            IsPocketOpen = true;
        }

        [RelayCommand]
        private void SelectPocketIcon(string iconCode)
        {
            PocketIconCode = iconCode;
            foreach (var icon in PocketIcons)
                icon.IsSelected = icon.Code == iconCode;
        }

        /// <summary>What the pocket holds, what its account has available, and the movements.</summary>
        private async Task RefreshPocketAsync()
        {
            var account = _pocketAccount;
            if (account == null)
                return;

            try
            {
                var code = PocketCurrencyCode;
                var inPockets = (await _pocketService.GetTotalsByAccountAsync()).GetValueOrDefault(account.Id);
                var available = await _balanceService.GetAccountBalanceAsync(account.Id) - Money.FromCents(inPockets);
                PocketAvailableText = string.Format(AppResources.PocketAccountAvailable, account.Name,
                    MoneyFormatter.Format(available, code));

                if (EditingPocket is { } pocket)
                {
                    var held = (await _pocketService.GetBalancesAsync()).GetValueOrDefault(pocket.Id);
                    PocketHoldsText = string.Format(AppResources.PocketHolds, MoneyFormatter.Format(Money.FromCents(held), code));
                    PocketMovements = (await _pocketService.GetMovementsAsync(pocket.Id))
                        .Select(m => new PocketMovementRow(m, code)).ToList();
                }
                else
                {
                    PocketHoldsText = string.Empty;
                    PocketMovements = [];
                }
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToLoadAccountBalances);
            }
        }

        [RelayCommand]
        private void ClosePocket()
        {
            IsPocketOpen = false;
            EditingPocket = null;
            _pocketAccount = null;
        }

        /// <summary>Saves the name and icon. A new pocket stays open, ready to have money set aside.</summary>
        [RelayCommand]
        private async Task SavePocketAsync()
        {
            var account = _pocketAccount;
            var name = PocketName.Trim();
            if (account == null)
                return;
            if (name.Length == 0)
            {
                await _errorHandler.HandleAsync(AppResources.PleaseEnterPocketName);
                return;
            }

            var adding = EditingPocket == null;
            var pocket = EditingPocket ?? new Pocket { AccountId = account.Id, CreatedDate = DateTime.Now };
            pocket.Name = name;
            pocket.IconCode = string.IsNullOrEmpty(PocketIconCode) ? pocket.IconCode : PocketIconCode;
            try
            {
                await _pocketService.SaveAsync(pocket);
                await LoadAccountsCommand.ExecuteAsync(null);
                if (adding)
                {
                    EditingPocket = pocket;
                    await RefreshPocketAsync();
                }
                else
                {
                    ClosePocket();
                }
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToSavePocket);
            }
        }

        [RelayCommand]
        private Task SetAsideAsync() => MovePocketAsync(1);

        [RelayCommand]
        private Task TakeOutAsync() => MovePocketAsync(-1);

        private async Task MovePocketAsync(int sign)
        {
            var pocket = EditingPocket;
            var account = _pocketAccount;
            if (pocket == null || account == null)
                return;

            var amount = AmountParser.Parse(PocketAmount, PocketCurrencyCode);
            if (amount <= 0 || !Money.IsInRange(amount))
            {
                await _errorHandler.HandleAsync(AppResources.PleaseEnterValidAmount);
                return;
            }

            try
            {
                await _pocketService.MoveAsync(pocket.Id, sign * amount, DateTime.Now);
                PocketAmount = string.Empty;
                await RefreshPocketAsync();
                await LoadAccountsCommand.ExecuteAsync(null);
            }
            catch (PocketProblemException ex)
            {
                await _errorHandler.HandleAsync(ex.Problem == PocketProblem.NotEnoughAvailable
                    ? string.Format(AppResources.PocketNotEnoughAvailable, account.Name)
                    : AppResources.PocketNotEnoughInPocket);
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToMovePocket);
            }
        }

        /// <summary>Deletes the pocket in the sheet; the sheet stays open behind the question, so cancelling returns to it.</summary>
        [RelayCommand]
        private async Task DeletePocketAsync()
        {
            var pocket = EditingPocket;
            var account = _pocketAccount;
            if (pocket == null || account == null)
                return;

            if (!await _dialogService.ConfirmAsync(AppResources.ConfirmDelete,
                    string.Format(AppResources.DeletePocketConfirmation, pocket.Name, account.Name),
                    AppResources.YesDelete, AppResources.Cancel))
                return;

            try
            {
                await _pocketService.DeleteAsync(pocket.Id);
                ClosePocket();
                await LoadAccountsCommand.ExecuteAsync(null);
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToDeletePocket);
            }
        }
    }
}
