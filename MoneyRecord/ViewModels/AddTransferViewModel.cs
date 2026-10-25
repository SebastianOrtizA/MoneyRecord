using MoneyRecord.Helpers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.ViewModels
{
    [QueryProperty(nameof(Transfer), "Transfer")]
    [QueryProperty(nameof(TransferIdString), "TransferId")]
    [QueryProperty(nameof(SourceAccountIdString), "SourceAccountId")]
    public partial class AddTransferViewModel : ObservableObject
    {
        private readonly IAccountRepository _accountRepository;
        private readonly ITransferRepository _transferRepository;
        private readonly IBalanceService _balanceService;
        private readonly IPocketService _pocketService;
        private readonly PocketCoverPrompt _pocketCover;
        private readonly INavigationService _navigationService;
        private readonly IErrorHandler _errorHandler;
        private readonly IDialogService _dialogService;

        [ObservableProperty]
        private Transfer? transfer;

        // Use string for query property to avoid type conversion issues with Shell navigation
        private string? _transferIdString;
        public string? TransferIdString
        {
            get => _transferIdString;
            set
            {
                _transferIdString = value;
                // Parse the string to int when set
                if (int.TryParse(value, out var id))
                {
                    TransferId = id;
                }
                else
                {
                    TransferId = null;
                }
            }
        }

        /// <summary>A new transfer from this account (the home-screen widget's button).</summary>
        public string? SourceAccountIdString { get; set; }

        [ObservableProperty]
        private int? transferId;

        [ObservableProperty]
        private DateTime selectedDate = DateTime.Now;

        [ObservableProperty]
        private string amount = string.Empty;

        [ObservableProperty]
        private string description = string.Empty;

        [ObservableProperty]
        private Account? selectedSourceAccount;

        [ObservableProperty]
        private Account? selectedDestinationAccount;

        [ObservableProperty]
        private List<Account> accounts = new();

        [ObservableProperty]
        private string title = string.Empty;

        [ObservableProperty]
        private bool isEditMode = false;

        [ObservableProperty]
        private decimal sourceAccountBalance = 0;

        [ObservableProperty]
        private string sourceAccountBalanceText = string.Empty;

        /// <summary>What arrived in a destination account of another currency (in its currency).</summary>
        [ObservableProperty]
        private string destinationAmount = string.Empty;

        /// <summary>The two accounts have different currencies: the form asks what arrived too.</summary>
        [ObservableProperty]
        private bool isCrossCurrency;

        [ObservableProperty]
        private string crossCurrencyHint = string.Empty;

        /// <summary>"Rate: 1 USD = 4,012.5 COP" from the two amounts; empty until both are entered.</summary>
        [ObservableProperty]
        private string rateText = string.Empty;

        // Store original transfer values for edit mode
        private int? _originalSourceAccountId;
        private int? _originalDestinationAccountId;
        private decimal _originalAmount;

        public AddTransferViewModel(IAccountRepository accountRepository, ITransferRepository transferRepository, IBalanceService balanceService, IPocketService pocketService, PocketCoverPrompt pocketCover, INavigationService navigationService, IErrorHandler errorHandler, IDialogService dialogService)
        {
            _pocketService = pocketService;
            _pocketCover = pocketCover;
            _accountRepository = accountRepository;
            _transferRepository = transferRepository;
            _balanceService = balanceService;
            _navigationService = navigationService;
            _errorHandler = errorHandler;
            _dialogService = dialogService;
        }

        /// <summary>
        /// Resets the ViewModel state for a fresh start
        /// </summary>
        private void ResetState()
        {
            Transfer = null;
            TransferId = null;
            _transferIdString = null;
            SelectedDate = DateTime.Now;
            Amount = string.Empty;
            DestinationAmount = string.Empty;
            Description = string.Empty;
            SelectedSourceAccount = null;
            SelectedDestinationAccount = null;
            Title = AppResources.NewTransfer;
            IsEditMode = false;
            SourceAccountBalance = 0;
            SourceAccountBalanceText = string.Empty;
            _originalSourceAccountId = null;
            _originalDestinationAccountId = null;
            _originalAmount = 0;
        }

        public async Task InitializeAsync()
        {
            // Reset state first to handle ViewModel reuse
            var savedTransferId = TransferId;
            var savedTransfer = Transfer;
            var sourceAccountId = int.TryParse(SourceAccountIdString, out var parsedId) ? parsedId : (int?)null;
            ResetState();
            TransferId = savedTransferId;
            Transfer = savedTransfer;

            // If TransferId was passed, load the transfer
            if (TransferId.HasValue && Transfer == null)
            {
                Transfer = await _transferRepository.GetByIdAsync(TransferId.Value);
            }

            await LoadAccountsAsync();

            if (Transfer != null)
            {
                // Edit mode
                IsEditMode = true;
                Title = AppResources.EditTransfer;
                SelectedDate = Transfer.Date;
                Description = Transfer.Description;

                // Store original values
                _originalSourceAccountId = Transfer.SourceAccountId;
                _originalDestinationAccountId = Transfer.DestinationAccountId;
                _originalAmount = Transfer.Amount;

                // Select accounts
                SelectedSourceAccount = Accounts.FirstOrDefault(a => a.Id == Transfer.SourceAccountId);
                SelectedDestinationAccount = Accounts.FirstOrDefault(a => a.Id == Transfer.DestinationAccountId);

                // After the accounts: the amount fields format in their currencies
                Amount = Transfer.Amount.ToString();
                if (IsCrossCurrency)
                    DestinationAmount = Transfer.DestinationAmount.ToString();
            }
            else
            {
                // Add mode
                IsEditMode = false;
                Title = AppResources.NewTransfer;
                Description = AppResources.DefaultTransferDescription;
                if (sourceAccountId != null)
                    SelectedSourceAccount = Accounts.FirstOrDefault(a => a.Id == sourceAccountId);
            }

            await UpdateSourceAccountBalanceAsync();
        }

        // The user's own accounts: a debt's transfers are edited on the Debts page
        private async Task LoadAccountsAsync()
        {
            Accounts = await _accountRepository.GetOwnAsync();
        }

        partial void OnSelectedSourceAccountChanged(Account? value)
        {
            _ = UpdateSourceAccountBalanceAsync();
            UpdateCrossCurrency();
        }

        partial void OnSelectedDestinationAccountChanged(Account? value)
        {
            UpdateCrossCurrency();
        }

        partial void OnAmountChanged(string value) => UpdateRate();

        partial void OnDestinationAmountChanged(string value) => UpdateRate();

        private void UpdateCrossCurrency()
        {
            var source = MoneyFormatter.ResolveCurrencyCode(SelectedSourceAccount?.CurrencyCode);
            var destination = MoneyFormatter.ResolveCurrencyCode(SelectedDestinationAccount?.CurrencyCode);
            IsCrossCurrency = SelectedSourceAccount != null && SelectedDestinationAccount != null && source != destination;
            CrossCurrencyHint = IsCrossCurrency
                ? string.Format(AppResources.CrossCurrencyTransferHint, source, destination)
                : string.Empty;
            UpdateRate();
        }

        private void UpdateRate()
        {
            var rate = IsCrossCurrency
                ? TransferRate.Describe(
                    AmountParser.Parse(Amount, SelectedSourceAccount?.CurrencyCode), SelectedSourceAccount?.CurrencyCode,
                    AmountParser.Parse(DestinationAmount, SelectedDestinationAccount?.CurrencyCode), SelectedDestinationAccount?.CurrencyCode)
                : null;
            RateText = rate == null ? string.Empty : string.Format(AppResources.ExchangeRateValue, rate);
        }

        private async Task UpdateSourceAccountBalanceAsync()
        {
            var account = SelectedSourceAccount;
            if (account == null)
            {
                SourceAccountBalance = 0;
                SourceAccountBalanceText = string.Empty;
                return;
            }

            try
            {
                var balance = await _balanceService.GetAccountBalanceAsync(account.Id);

                // Another account was picked meanwhile: its own update shows its balance
                if (account != SelectedSourceAccount)
                    return;

                // If editing, add back the original transfer amount if this was the source
                if (IsEditMode && _originalSourceAccountId == account.Id)
                {
                    balance += _originalAmount;
                }

                SourceAccountBalance = balance;
                SourceAccountBalanceText = string.Format(AppResources.Available, MoneyFormatter.Format(balance, account.CurrencyCode));
                // Money set aside inside the account is still in its balance: say how much
                var inPockets = (await _pocketService.GetPocketsAsync(account.Id)).Sum(p => p.Balance);
                if (inPockets > 0 && account == SelectedSourceAccount)
                    SourceAccountBalanceText += " · " + string.Format(AppResources.InPockets,
                        MoneyFormatter.Format(inPockets, account.CurrencyCode));
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToLoadAccountBalances);
            }
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            if (SelectedSourceAccount == null)
            {
                await _errorHandler.HandleAsync(AppResources.PleaseSelectSourceAccount);
                return;
            }

            if (SelectedDestinationAccount == null)
            {
                await _errorHandler.HandleAsync(AppResources.PleaseSelectDestinationAccount);
                return;
            }

            if (SelectedSourceAccount.Id == SelectedDestinationAccount.Id)
            {
                await _errorHandler.HandleAsync(AppResources.SourceDestinationMustBeDifferent);
                return;
            }

            var amountValue = AmountParser.Parse(Amount, SelectedSourceAccount.CurrencyCode);
            if (amountValue <= 0 || !Money.IsInRange(amountValue))
            {
                await _errorHandler.HandleAsync(AppResources.PleaseEnterValidAmount);
                return;
            }

            // Check available balance if account doesn't allow negative balance
            if (!SelectedSourceAccount.AllowNegativeBalance && amountValue > SourceAccountBalance)
            {
                var message = string.Format(AppResources.InsufficientAccountBalance, SelectedSourceAccount.Name);
                await _errorHandler.HandleAsync(message);
                return;
            }

            // Between currencies, what arrived is entered separately; otherwise it is what left
            var destinationValue = amountValue;
            if (IsCrossCurrency)
            {
                destinationValue = AmountParser.Parse(DestinationAmount, SelectedDestinationAccount.CurrencyCode);
                if (destinationValue <= 0 || !Money.IsInRange(destinationValue))
                {
                    await _errorHandler.HandleAsync(AppResources.PleaseEnterValidAmount);
                    return;
                }
            }

            var description = string.IsNullOrWhiteSpace(Description) ? AppResources.DefaultTransferDescription : Description;

            // A transfer out that takes money set aside in pockets: ask which pocket to take it from
            var originalOutflow = IsEditMode && Transfer != null && _originalSourceAccountId == SelectedSourceAccount.Id
                && Transfer.Date < _balanceService.CountedBefore ? _originalAmount : 0;
            var cover = await _pocketCover.AskAsync(SelectedSourceAccount.Id, amountValue - originalOutflow, SelectedDate);
            if (!cover.Proceed)
                return;
            int? savedTransferId = null;

            try
            {
                if (IsEditMode && Transfer != null)
                {
                    // Update existing transfer
                    Transfer.Date = SelectedDate;
                    Transfer.Amount = amountValue;
                    Transfer.DestinationAmount = destinationValue;
                    Transfer.Description = description;
                    Transfer.SourceAccountId = SelectedSourceAccount.Id;
                    Transfer.DestinationAccountId = SelectedDestinationAccount.Id;

                    await _transferRepository.SaveAsync(Transfer);
                    savedTransferId = Transfer.Id;
                    await _dialogService.ToastAsync(AppResources.TransferUpdatedSuccessfully);
                }
                else
                {
                    // Create new transfer
                    var newTransfer = new Transfer
                    {
                        Date = SelectedDate,
                        Amount = amountValue,
                        DestinationAmount = destinationValue,
                        Description = description,
                        SourceAccountId = SelectedSourceAccount.Id,
                        DestinationAccountId = SelectedDestinationAccount.Id
                    };

                    await _transferRepository.SaveAsync(newTransfer);
                    savedTransferId = newTransfer.Id;
                }

                await RecordCoverAsync(SelectedSourceAccount.Id, cover, savedTransferId);
                await _navigationService.GoBackAsync();
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToSaveTransfer);
            }
        }

        /// <summary>Records the pockets' take-outs of a saved transfer; best-effort, the transfer is saved already.</summary>
        private async Task RecordCoverAsync(int accountId, PocketCoverChoice cover, int? transferId)
        {
            if (cover.Takes.Count == 0)
                return;

            try
            {
                await _pocketService.CoverAsync(accountId, cover.Takes, SelectedDate, transferId: transferId);
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToMovePocket);
            }
        }

        [RelayCommand]
        private async Task CancelAsync()
        {
            await _navigationService.GoBackAsync();
        }
    }
}
