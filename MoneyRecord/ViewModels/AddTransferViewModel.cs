using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyRecord.Behaviors;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services;

namespace MoneyRecord.ViewModels
{
    [QueryProperty(nameof(Transfer), "Transfer")]
    [QueryProperty(nameof(TransferIdString), "TransferId")]
    public partial class AddTransferViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;

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
        private string title = "New Transfer";

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

        public AddTransferViewModel(DatabaseService databaseService)
        {
            _databaseService = databaseService;
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
            Title = "New Transfer";
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
                Transfer = await _databaseService.GetTransferAsync(TransferId.Value);
            }

            if (Transfer != null)
            {
                // Edit mode
                IsEditMode = true;
                Title = "Edit Transfer";
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
                Title = "New Transfer";
                Description = "Transfer";
            }

            await UpdateSourceAccountBalanceAsync();
        }

        private async Task LoadAccountsAsync()
        {
            Accounts = await _databaseService.GetAccountsAsync();
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
                var balance = await _databaseService.GetAccountBalanceAsync(SelectedSourceAccount.Id);

                // If editing, add back the original transfer amount if this was the source
                if (IsEditMode && _originalSourceAccountId == SelectedSourceAccount.Id)
                {
                    balance += _originalAmount;
                }

                SourceAccountBalance = balance;
                SourceAccountBalanceText = $"Available: ${balance:N2}";
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
                await Shell.Current.DisplayAlertAsync(AppResources.Error, AppResources.PleaseSelectSourceAccount, AppResources.OK);
                return;
            }

            if (SelectedDestinationAccount == null)
            {
                await Shell.Current.DisplayAlertAsync(AppResources.Error, AppResources.PleaseSelectDestinationAccount, AppResources.OK);
                return;
            }

            if (SelectedSourceAccount.Id == SelectedDestinationAccount.Id)
            {
                await Shell.Current.DisplayAlertAsync(AppResources.Error, AppResources.SourceDestinationMustBeDifferent, AppResources.OK);
                return;
            }

            var amountValue = CurrencyMaskBehavior.ParseCurrencyValue(Amount);
            if (amountValue <= 0)
            {
                await Shell.Current.DisplayAlertAsync(AppResources.Error, AppResources.PleaseEnterValidAmount, AppResources.OK);
                return;
            }

            // Check available balance
            if (amountValue > SourceAccountBalance)
            {
                await Shell.Current.DisplayAlertAsync(AppResources.Error, AppResources.InsufficientFunds, AppResources.OK);
                return;
            }

            try
            {
                if (IsEditMode && Transfer != null)
                {
                    // Update existing transfer
                    Transfer.Date = SelectedDate;
                    Transfer.Amount = amountValue;
                    Transfer.Description = string.IsNullOrWhiteSpace(Description) ? "Transfer" : Description;
                    Transfer.SourceAccountId = SelectedSourceAccount.Id;
                    Transfer.DestinationAccountId = SelectedDestinationAccount.Id;

                    await _databaseService.SaveTransferAsync(Transfer);
                    await Shell.Current.DisplayAlertAsync("Success", "Transfer updated successfully", "OK");
                }
                else
                {
                    // Create new transfer
                    var newTransfer = new Transfer
                    {
                        Date = SelectedDate,
                        Amount = amountValue,
                        Description = string.IsNullOrWhiteSpace(Description) ? "Transfer" : Description,
                        SourceAccountId = SelectedSourceAccount.Id,
                        DestinationAccountId = SelectedDestinationAccount.Id
                    };

                    await _databaseService.SaveTransferAsync(newTransfer);
                }

                await Shell.Current.GoToAsync("..");
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlertAsync("Error", $"Failed to save transfer: {ex.Message}", "OK");
            }
        }

        [RelayCommand]
        private async Task CancelAsync()
        {
            await Shell.Current.GoToAsync("..");
        }
    }
}
