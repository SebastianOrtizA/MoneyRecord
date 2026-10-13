using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.Views;

namespace MoneyRecord.ViewModels
{
    /// <summary>
    /// Transactions tab: the period's transactions and transfers with search, filter chips, grouping and sort. A tap opens
    /// the entry's detail sheet; a swipe edits or deletes it.
    /// </summary>
    public partial class TransactionsViewModel : ObservableObject
    {
        private readonly IBalanceService _balanceService;
        private readonly ILedgerService _ledgerService;
        private readonly ITransactionRepository _transactionRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly IErrorHandler _errorHandler;
        private readonly IDialogService _dialogService;
        private readonly INavigationService _navigationService;

        private List<LedgerEntry> _entries = [];
        private List<LedgerEntry> _accountEntries = [];
        private List<AccountBalanceInfo> _accountBalances = [];
        private List<Account> _accounts = [];

        // TODO(8.7): Home and Transactions each keep their own period (same picker and logic as MainViewModel);
        // the shared period picker replaces both
        [ObservableProperty]
        private PeriodItem selectedPeriod;

        [ObservableProperty]
        private DateTime customStartDate = DateTime.Now.AddMonths(-1);

        [ObservableProperty]
        private DateTime customEndDate = DateTime.Now;

        [ObservableProperty]
        private bool isCustomPeriodSelected;

        [ObservableProperty]
        private GroupingMode currentGroupingMode = GroupingMode.Category;

        [ObservableProperty]
        private bool isAscending;

        [ObservableProperty]
        private bool isRefreshing;

        /// <summary>The rows: <see cref="LedgerEntry"/> items, or <see cref="TransactionGroup"/> items when grouped.</summary>
        [ObservableProperty]
        private IReadOnlyList<object> items = [];

        /// <summary>True once a load found nothing to show (before the first load the list is just not there yet).</summary>
        [ObservableProperty]
        private bool isEmpty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(EmptyTitle), nameof(EmptyHint))]
        private bool hasActiveFilters;

        /// <summary>"12 of 30" while a filter is active.</summary>
        [ObservableProperty]
        private string filteredCountText = string.Empty;

        /// <summary>The entry shown in the detail sheet; null = closed.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsDetailsOpen))]
        private LedgerEntry? selectedEntry;

        /// <summary>The selected entry's receipt shown full screen.</summary>
        [ObservableProperty]
        private bool isReceiptOpen;

        public List<PeriodItem> Periods { get; } = PeriodHelper.GetPeriods();

        public TransactionFilterViewModel Filters { get; }

        public bool IsDetailsOpen => SelectedEntry != null;

        public string EmptyTitle => HasActiveFilters ? AppResources.NoMatchingTransactions : AppResources.NoTransactionsInPeriod;

        public string EmptyHint => HasActiveFilters ? AppResources.TryOtherFilters : AppResources.AddEntryHint;

        public TransactionsViewModel(IBalanceService balanceService, ILedgerService ledgerService, ITransactionRepository transactionRepository,
            IAccountRepository accountRepository, IErrorHandler errorHandler, IDialogService dialogService, INavigationService navigationService)
        {
            _balanceService = balanceService;
            _ledgerService = ledgerService;
            _transactionRepository = transactionRepository;
            _accountRepository = accountRepository;
            _errorHandler = errorHandler;
            _dialogService = dialogService;
            _navigationService = navigationService;
            selectedPeriod = PeriodHelper.GetDefaultPeriod();
            Filters = new TransactionFilterViewModel(dialogService);
            Filters.Changed += ApplyFilters;
        }

        public Task InitializeAsync() => LoadDataCommand.ExecuteAsync(null);

        /// <summary>Reloads the list. Each run cancels the one in progress, so an older period never wins.</summary>
        [RelayCommand(AllowConcurrentExecutions = true)]
        private async Task LoadDataAsync(CancellationToken cancellationToken)
        {
            try
            {
                IsRefreshing = true;

                var (startDate, endDate) = DateRangeHelper.GetDateRange(SelectedPeriod?.Type, CustomStartDate, CustomEndDate);
                var balancesTask = _balanceService.GetAllAccountBalancesAsync();
                var entriesTask = _ledgerService.GetEntriesAsync(startDate, endDate);
                var accountsTask = _accountRepository.GetAllAsync();
                await Task.WhenAll(balancesTask, entriesTask, accountsTask);
                cancellationToken.ThrowIfCancellationRequested();

                // Whole transfers for the other views; split once for the account view, so switching the grouping
                // never reloads
                _accountBalances = await balancesTask;
                _entries = await entriesTask;
                _accountEntries = LedgerTransfers.PerAccount(_entries);
                _accounts = await accountsTask;
                Filters.SetOptions(_entries);
                ApplyFilters();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // A newer load replaced this one
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToLoadTransactions);
            }
            finally
            {
                // The newer load owns the spinner
                if (!cancellationToken.IsCancellationRequested)
                    IsRefreshing = false;
            }
        }

        private void ApplyFilters()
        {
            // Only the account view lists a transfer under both of its accounts
            var entries = CurrentGroupingMode == GroupingMode.Account ? _accountEntries : _entries;
            var filter = Filters.ToFilter(IsAscending);
            var filtered = LedgerFilter.Apply(entries, filter);

            HasActiveFilters = filter.IsActive;
            FilteredCountText = string.Format(AppResources.FilteredOfTotal, filtered.Count, entries.Count);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                Items = CurrentGroupingMode == GroupingMode.None
                    ? (IReadOnlyList<object>)filtered
                    : LedgerGrouping.Group(filtered, CurrentGroupingMode, _accountBalances, _accounts);
                IsEmpty = filtered.Count == 0;
            });
        }

        [RelayCommand]
        private void ToggleSortOrder()
        {
            IsAscending = !IsAscending;
            ApplyFilters();
        }

        [RelayCommand]
        private void ToggleGrouping()
        {
            // None -> Category -> Account -> None
            CurrentGroupingMode = CurrentGroupingMode switch
            {
                GroupingMode.None => GroupingMode.Category,
                GroupingMode.Category => GroupingMode.Account,
                _ => GroupingMode.None
            };
            ApplyFilters();
        }

        [RelayCommand]
        private void ToggleGroupExpanded(TransactionGroup group) => group?.ToggleExpanded();

        [RelayCommand]
        private void ShowDetails(LedgerEntry entry) => SelectedEntry = entry;

        [RelayCommand]
        private void CloseDetails()
        {
            IsReceiptOpen = false;
            SelectedEntry = null;
        }

        [RelayCommand]
        private void OpenReceipt() => IsReceiptOpen = SelectedEntry?.HasReceipt == true;

        [RelayCommand]
        private void CloseReceipt() => IsReceiptOpen = false;

        [RelayCommand]
        private Task EditSelectedAsync()
        {
            var entry = SelectedEntry;
            CloseDetails();
            return EditTransactionAsync(entry);
        }

        [RelayCommand]
        private Task DeleteSelectedAsync()
        {
            var entry = SelectedEntry;
            CloseDetails();
            return DeleteTransactionAsync(entry);
        }

        /// <summary>Closes the full-screen receipt, else the detail sheet; false when neither was open.</summary>
        public bool CloseTopOverlay()
        {
            if (IsReceiptOpen)
            {
                CloseReceipt();
                return true;
            }

            if (!IsDetailsOpen)
                return false;

            CloseDetails();
            return true;
        }

        [RelayCommand]
        private async Task EditTransactionAsync(LedgerEntry? entry)
        {
            if (entry == null)
                return;

            if (entry.IsTransfer)
            {
                await _navigationService.NavigateToAsync($"{nameof(AddTransferPage)}?TransferId={entry.SourceId}");
                return;
            }

            var transaction = await _transactionRepository.GetByIdAsync(entry.SourceId);
            if (transaction != null)
            {
                await _navigationService.NavigateToAsync(nameof(AddTransactionPage),
                    new Dictionary<string, object> { { "Transaction", transaction } });
            }
        }

        [RelayCommand]
        private async Task DeleteTransactionAsync(LedgerEntry? entry)
        {
            if (entry == null)
                return;

            var amount = MoneyFormatter.Format(entry.Amount, entry.CurrencyCode);
            var confirmMessage = entry.IsTransfer
                ? string.Format(AppResources.ConfirmDeleteTransferFromListMessage, entry.Description, amount)
                : string.Format(AppResources.ConfirmDeleteTransactionMessage, entry.Description, amount);

            if (!await _dialogService.ConfirmAsync(AppResources.ConfirmDeleteTransactionTitle, confirmMessage,
                    AppResources.YesDelete, AppResources.Cancel))
                return;

            try
            {
                await _ledgerService.DeleteAsync(entry);
                await LoadDataCommand.ExecuteAsync(null);

                await _dialogService.ToastAsync(entry.IsTransfer
                    ? AppResources.TransferDeletedSuccessfully
                    : AppResources.TransactionDeletedSuccessfully);
            }
            catch (Exception ex)
            {
                var errorMessage = entry.IsTransfer
                    ? AppResources.FailedToDeleteTransfer
                    : AppResources.FailedToDeleteTransaction;
                await _errorHandler.HandleAsync(ex, errorMessage);
            }
        }

        partial void OnSelectedPeriodChanged(PeriodItem value)
        {
            IsCustomPeriodSelected = value?.Type == PeriodType.CustomPeriod;
            LoadDataCommand.Execute(null);
        }

        partial void OnCustomStartDateChanged(DateTime value)
        {
            if (IsCustomPeriodSelected)
                LoadDataCommand.Execute(null);
        }

        partial void OnCustomEndDateChanged(DateTime value)
        {
            if (IsCustomPeriodSelected)
                LoadDataCommand.Execute(null);
        }
    }
}
