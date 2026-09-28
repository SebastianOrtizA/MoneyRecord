using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyRecord.Behaviors;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.ViewModels
{
    [QueryProperty(nameof(Transfer), "Transfer")]
    [QueryProperty(nameof(TransferIdString), "TransferId")]
    public partial class AddTransferViewModel : ObservableObject
    {
        private readonly IAccountRepository _accountRepository;
        private readonly ITransferRepository _transferRepository;
        private readonly IBalanceService _balanceService;
        private readonly INavigationService _navigationService;
        private readonly IErrorHandler _errorHandler;

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

        // Store original transfer values for edit mode
        private int? _originalSourceAccountId;
        private int? _originalDestinationAccountId;
        private decimal _originalAmount;

        public AddTransferViewModel(IAccountRepository accountRepository, ITransferRepository transferRepository, IBalanceService balanceService, INavigationService navigationService, IErrorHandler errorHandler)
        {
            _accountRepository = accountRepository;
            _transferRepository = transferRepository;
            _balanceService = balanceService;
            _navigationService = navigationService;
            _errorHandler = errorHandler;
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
            ResetState();
            TransferId = savedTransferId;
            Transfer = savedTransfer;

            await LoadAccountsAsync();

            // If TransferId was passed, load the transfer
            if (TransferId.HasValue && Transfer == null)
            {
                Transfer = await _transferRepository.GetByIdAsync(TransferId.Value);
            }

            if (Transfer != null)
            {
                // Edit mode
                IsEditMode = true;
                Title = AppResources.EditTransfer;
                SelectedDate = Transfer.Date;
                Amount = Transfer.Amount.ToString();
                Description = Transfer.Description;

                // Store original values
                _originalSourceAccountId = Transfer.SourceAccountId;
                _originalDestinationAccountId = Transfer.DestinationAccountId;
                _originalAmount = Transfer.Amount;

                // Select accounts
                SelectedSourceAccount = Accounts.FirstOrDefault(a => a.Id == Transfer.SourceAccountId);
                SelectedDestinationAccount = Accounts.FirstOrDefault(a => a.Id == Transfer.DestinationAccountId);
            }
            else
            {
                // Add mode
                IsEditMode = false;
                Title = AppResources.NewTransfer;
                Description = AppResources.Transfer;
            }

            await UpdateSourceAccountBalanceAsync();
        }

        private async Task LoadAccountsAsync()
        {
            Accounts = await _accountRepository.GetAllAsync();
        }

        partial void OnSelectedSourceAccountChanged(Account? value)
        {
            _ = UpdateSourceAccountBalanceAsync();
        }

        private async Task UpdateSourceAccountBalanceAsync()
        {
            if (SelectedSourceAccount != null)
            {
                // Get base balance
                var balance = await _balanceService.GetAccountBalanceAsync(SelectedSourceAccount.Id);

                // If editing, add back the original transfer amount if this was the source
                if (IsEditMode && _originalSourceAccountId == SelectedSourceAccount.Id)
                {
                    balance += _originalAmount;
                }

                SourceAccountBalance = balance;
                SourceAccountBalanceText = string.Format(AppResources.Available, balance);
            }
            else
            {
                SourceAccountBalance = 0;
                SourceAccountBalanceText = string.Empty;
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

            var amountValue = CurrencyMaskBehavior.ParseCurrencyValue(Amount);
            if (amountValue <= 0)
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

            try
            {
                if (IsEditMode && Transfer != null)
                {
                    // Update existing transfer
                    Transfer.Date = SelectedDate;
                    Transfer.Amount = amountValue;
                    Transfer.Description = string.IsNullOrWhiteSpace(Description) ? AppResources.Transfer : Description;
                    Transfer.SourceAccountId = SelectedSourceAccount.Id;
                    Transfer.DestinationAccountId = SelectedDestinationAccount.Id;

                    await _transferRepository.SaveAsync(Transfer);
                    await Toast.Make(AppResources.TransferUpdatedSuccessfully).Show();
                }
                else
                {
                    // Create new transfer
                    var newTransfer = new Transfer
                    {
                        Date = SelectedDate,
                        Amount = amountValue,
                        Description = string.IsNullOrWhiteSpace(Description) ? AppResources.Transfer : Description,
                        SourceAccountId = SelectedSourceAccount.Id,
                        DestinationAccountId = SelectedDestinationAccount.Id
                    };

                    await _transferRepository.SaveAsync(newTransfer);
                }

                await _navigationService.GoBackAsync();
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, string.Format(AppResources.FailedToSaveTransfer, ex.Message));
            }
        }

        [RelayCommand]
        private async Task CancelAsync()
        {
            await _navigationService.GoBackAsync();
        }
    }
}
