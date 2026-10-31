using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.Views;

namespace MoneyRecord.ViewModels
{
    /// <summary>
    /// Transactions tab: the period's transactions and transfers with search, filter chips, grouping and sort. A tap opens
    /// the entry's detail sheet; a swipe edits or deletes it.
    /// </summary>
    public partial class TransactionsViewModel : ObservableObject, IFilterSheetSource
    {
        private readonly IBalanceService _balanceService;
        private readonly ILedgerService _ledgerService;
        private readonly ITransactionsViewSettings _viewSettings;
        private readonly ISwipeHintSettings _swipeHint;
        private readonly ITransactionRepository _transactionRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly IErrorHandler _errorHandler;
        private readonly IDialogService _dialogService;
        private readonly INavigationService _navigationService;
        private readonly TransactionsDrillDown _drillDown;

        // While the page is shown: a period change (here or on another tab) reloads it
        private IDisposable? _periodWatch;

        private List<LedgerEntry> _entries = [];
        private List<LedgerEntry> _accountEntries = [];
        private List<AccountBalanceInfo> _accountBalances = [];
        private List<Account> _accounts = [];

        /// <summary>How the list is grouped (saved: by date until the user picks another).</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowRowDates), nameof(ByDateText), nameof(ByCategoryText), nameof(ByAccountText), nameof(ListText))]
        private GroupingMode currentGroupingMode;

        // The overflow menu marks the grouping in use
        public string ByDateText => MenuText(GroupingMode.Date, AppResources.GroupByDate);

        public string ByCategoryText => MenuText(GroupingMode.Category, AppResources.GroupByCategory);

        public string ByAccountText => MenuText(GroupingMode.Account, AppResources.GroupByAccount);

        public string ListText => MenuText(GroupingMode.None, AppResources.ListView);

        private string MenuText(GroupingMode mode, string text) => CurrentGroupingMode == mode ? $"✓ {text}" : text;

        /// <summary>A day group names the date once, so its rows leave it out.</summary>
        public bool ShowRowDates => CurrentGroupingMode != GroupingMode.Date;

        partial void OnCurrentGroupingModeChanged(GroupingMode value) => _viewSettings.Grouping = value;

        /// <summary>The search box is shown (opened by the toolbar's search icon).</summary>
        [ObservableProperty]
        private bool isSearchOpen;

        [RelayCommand]
        private void ToggleSearch() => IsSearchOpen = !IsSearchOpen;

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

        /// <summary>Every part of the selected split payment (empty when it isn't one), for the detail sheet.</summary>
        [ObservableProperty]
        private List<LedgerEntry> selectedSplitParts = [];

        /// <summary>"Split payment of $50.00 in 2 parts"; empty when the selected entry isn't split.</summary>
        [ObservableProperty]
        private string splitSummaryText = string.Empty;

        /// <summary>"Lisbon · Lodging" for the selected expense of a trip; empty otherwise.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasTripText))]
        private string tripText = string.Empty;

        public bool HasTripText => TripText.Length > 0;

        private readonly ITripService _trips;

        private async Task LoadTripTextAsync(LedgerEntry? entry)
        {
            TripText = string.Empty;
            if (entry?.TripId is not { } tripId)
                return;

            try
            {
                var trip = await _trips.GetAsync(tripId);
                var category = (await _trips.GetCategoriesAsync()).FirstOrDefault(c => c.Id == entry.TripCategoryId);
                if (trip != null && SelectedEntry == entry)
                    TripText = category == null ? trip.Name : $"{trip.Name} · {category.Name}";
            }
            catch (Exception)
            {
                // Only an extra line in the sheet
            }
        }

        partial void OnSelectedEntryChanged(LedgerEntry? value)
        {
            _ = LoadTripTextAsync(value);
            SelectedSplitParts = SplitPartsOf(value);
            SplitSummaryText = SelectedSplitParts.Count > 0
                ? string.Format(AppResources.SplitPaymentTotal, SplitTotalText(SelectedSplitParts), SelectedSplitParts.Count)
                : string.Empty;
        }

        /// <summary>The selected entry's receipt shown full screen.</summary>
        [ObservableProperty]
        private bool isReceiptOpen;

        /// <summary>The "swipe a row" caption above the list.</summary>
        [ObservableProperty]
        private bool showSwipeHint;

        private bool _swipeHintDecided;

        public TransactionFilterViewModel Filters { get; }

        public bool IsDetailsOpen => SelectedEntry != null;

        public string EmptyTitle => HasActiveFilters ? AppResources.NoMatchingTransactions : AppResources.NoTransactionsInPeriod;

        public string EmptyHint => HasActiveFilters ? AppResources.TryOtherFilters : AppResources.AddEntryHint;

        /// <summary>The shared period (Home, Transactions, Budgets, Spending / Income).</summary>
        public PeriodViewModel Period { get; }

        public TransactionsViewModel(PeriodViewModel period, IBalanceService balanceService, ILedgerService ledgerService, ITransactionRepository transactionRepository,
            IAccountRepository accountRepository, IErrorHandler errorHandler, IDialogService dialogService, INavigationService navigationService,
            TransactionsDrillDown drillDown, ITripService trips, ISwipeHintSettings swipeHint, ITransactionsViewSettings viewSettings)
        {
            _swipeHint = swipeHint;
            _viewSettings = viewSettings;
            currentGroupingMode = viewSettings.Grouping;
            _trips = trips;
            _balanceService = balanceService;
            _ledgerService = ledgerService;
            _transactionRepository = transactionRepository;
            _accountRepository = accountRepository;
            _errorHandler = errorHandler;
            _dialogService = dialogService;
            _navigationService = navigationService;
            _drillDown = drillDown;
            Filters = new TransactionFilterViewModel(dialogService);
            Filters.Changed += ApplyFilters;
            // A pick settles the filter: the sheet closes, as the period sheet does
            Filters.Picked += CloseFilter;
            Period = period;
        }

        /// <summary>The page appeared: load, and follow the shared period until <see cref="Deactivate"/>.</summary>
        public Task InitializeAsync()
        {
            _swipeHintDecided = false;
            _periodWatch ??= Period.WatchChanges(() => LoadDataCommand.Execute(null));
            return LoadDataCommand.ExecuteAsync(null);
        }

        /// <summary>A row was swiped: the caption is no longer needed.</summary>
        public void MarkSwipeSeen()
        {
            _swipeHint.MarkSeen();
            ShowSwipeHint = false;
        }

        /// <summary>The page disappeared: stop following the period (it reloads when it appears again).</summary>
        public void Deactivate()
        {
            _periodWatch?.Dispose();
            _periodWatch = null;
        }

        /// <summary>Reloads the list. Each run cancels the one in progress, so an older period never wins.</summary>
        [RelayCommand(AllowConcurrentExecutions = true)]
        private async Task LoadDataAsync(CancellationToken cancellationToken)
        {
            try
            {
                IsRefreshing = true;

                var (startDate, endDate) = Period.Range;
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
                // A report opened this tab for one category or type: show only that (ShowOnly applies the filters)
                if (_drillDown.TryTake(out var categoryId, out var type))
                    Filters.ShowOnly(type, categoryId);
                else
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
            OnPropertyChanged(nameof(ActiveFilterCount));
            OnPropertyChanged(nameof(HasActiveFilterCount));
            FilteredCountText = string.Format(AppResources.FilteredOfTotal, filtered.Count, entries.Count);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                Items = CurrentGroupingMode == GroupingMode.None
                    ? (IReadOnlyList<object>)filtered
                    : LedgerGrouping.Group(filtered, CurrentGroupingMode, _accountBalances, _accounts, IsAscending);
                IsEmpty = filtered.Count == 0;

                // Once per visit, when there are rows to swipe
                if (!_swipeHintDecided && filtered.Count > 0)
                {
                    _swipeHintDecided = true;
                    ShowSwipeHint = _swipeHint.ShouldShow;
                    if (ShowSwipeHint)
                        _swipeHint.RecordVisit();
                }
            });
        }

        [RelayCommand]
        private void ToggleSortOrder()
        {
            IsAscending = !IsAscending;
            ApplyFilters();
        }

        /// <summary>The overflow menu: group by date, category or account, or no groups.</summary>
        [RelayCommand]
        private void SetGrouping(GroupingMode mode)
        {
            if (mode == CurrentGroupingMode)
                return;

            CurrentGroupingMode = mode;
            ApplyFilters();
        }

        [RelayCommand]
        private void ToggleGroupExpanded(TransactionGroup group) => group?.ToggleExpanded();

        private List<LedgerEntry> SplitPartsOf(LedgerEntry? entry) => entry is { IsSplit: true }
            ? _entries.Where(e => e.SplitId == entry.SplitId).OrderBy(e => e.SourceId).ToList()
            : [];

        private static string SplitTotalText(List<LedgerEntry> parts) =>
            MoneyFormatter.Format(Money.FromCents(parts.Sum(p => p.AmountCents)), parts[0].CurrencyCode);

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

        /// <summary>The sheet stays open behind the question, so cancelling returns to the entry.</summary>
        [RelayCommand]
        private async Task DeleteSelectedAsync()
        {
            if (await TryDeleteAsync(SelectedEntry))
                CloseDetails();
        }

        /// <summary>The filter sheet (type, category, account, tag, amount), opened by the page's filter button.</summary>
        [ObservableProperty]
        private bool isFilterOpen;

        public int ActiveFilterCount => Filters.ActiveCount;

        public bool HasActiveFilterCount => ActiveFilterCount > 0;

        [RelayCommand]
        private void OpenFilter() => IsFilterOpen = true;

        [RelayCommand]
        private void CloseFilter() => IsFilterOpen = false;

        IRelayCommand IFilterSheetSource.ClearFiltersCommand => Filters.ClearCommand;

        /// <summary>Closes the full-screen receipt, else the filter sheet, else the detail sheet; false when none was open.</summary>
        public bool CloseTopOverlay()
        {
            if (IsReceiptOpen)
            {
                CloseReceipt();
                return true;
            }

            if (IsFilterOpen)
            {
                CloseFilter();
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

            // A debt's money is edited on the Debts page, where a payment can't exceed what is left
            if (entry.IsTransfer && _accounts.Any(a => a.IsDebt && (a.Id == entry.AccountId || a.Id == entry.CounterpartAccountId)))
            {
                await _navigationService.NavigateToAsync(nameof(DebtsPage));
                await _dialogService.ToastAsync(AppResources.DebtEditOnDebtsPage);
                return;
            }

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
        private Task DeleteTransactionAsync(LedgerEntry? entry) => TryDeleteAsync(entry);

        /// <summary>Deletes at once and offers Undo; true when the entry is gone (not when the delete failed).</summary>
        private async Task<bool> TryDeleteAsync(LedgerEntry? entry)
        {
            if (entry == null)
                return false;

            try
            {
                var deleted = await _ledgerService.DeleteAsync(entry);
                await LoadDataCommand.ExecuteAsync(null);

                if (deleted != null)
                    await DeleteUndo.OfferAsync(_dialogService, _ledgerService, _errorHandler, deleted, DeletedMessage(entry, deleted),
                        () => LoadDataCommand.ExecuteAsync(null));
                return true;
            }
            catch (Exception ex)
            {
                var errorMessage = entry.IsTransfer
                    ? AppResources.FailedToDeleteTransfer
                    : AppResources.FailedToDeleteTransaction;
                await _errorHandler.HandleAsync(ex, errorMessage);
                return false;
            }
        }

        private static string DeletedMessage(LedgerEntry entry, DeletedEntry deleted) =>
            entry.IsTransfer ? AppResources.TransferDeletedUndo
            : deleted.Transactions.Count > 1 ? string.Format(AppResources.SplitDeletedUndo, deleted.Transactions.Count)
            : entry.Kind == LedgerEntryKind.Income ? AppResources.IncomeDeletedUndo
            : AppResources.ExpenseDeletedUndo;
    }
}
