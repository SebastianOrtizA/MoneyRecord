using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.ViewModels
{
    /// <summary>One trip category in the End trip sheet: the real category its expenses move to.</summary>
    public partial class TripEndRow(TripCategory category, List<Category> choices, Category? selected) : ObservableObject
    {
        public TripCategory Source { get; } = category;

        // On the row itself: a Picker in a BindableLayout template needs its ItemsSource on the item (an ancestor binding
        // resolves after SelectedItem and clears it)
        public List<Category> Choices { get; } = choices;

        [ObservableProperty]
        private Category? selected = selected;
    }

    /// <summary>One trip: spent against its budget, per trip category, and its expenses.</summary>
    [QueryProperty(nameof(TripIdText), "TripId")]
    public partial class TripViewModel : ObservableObject
    {
        private readonly ITripService _trips;
        private readonly IAccountRepository _accountRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly INavigationService _navigation;
        private readonly IErrorHandler _errorHandler;
        private readonly IDialogService _dialogService;

        public string? TripIdText { get; set; }

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasTrip), nameof(Title), nameof(IsActiveTrip), nameof(IsEndedTrip))]
        private TripCard? card;

        public bool HasTrip => Card != null;

        public bool IsActiveTrip => Card is { IsActive: true };

        public bool IsEndedTrip => Card is { IsActive: false };

        public string Title => Card?.Name ?? AppResources.TripsTitle;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasCategories))]
        private List<TripCategoryRow> categories = [];

        public bool HasCategories => Categories.Count > 0;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasEntries))]
        private List<TripEntryRow> entries = [];

        public bool HasEntries => Entries.Count > 0;

        public TripViewModel(ITripService trips, IAccountRepository accountRepository, ICategoryRepository categoryRepository, INavigationService navigation,
            IErrorHandler errorHandler, IDialogService dialogService)
        {
            _trips = trips;
            _accountRepository = accountRepository;
            _categoryRepository = categoryRepository;
            _navigation = navigation;
            _errorHandler = errorHandler;
            _dialogService = dialogService;
        }

        public Task InitializeAsync() => LoadCommand.ExecuteAsync(null);

        [RelayCommand(AllowConcurrentExecutions = true)]
        private async Task LoadAsync(CancellationToken cancellationToken)
        {
            if (!int.TryParse(TripIdText, out var tripId))
                return;

            try
            {
                IsLoading = true;
                var summary = await _trips.GetSummaryAsync(tripId);
                var expenses = await _trips.GetExpensesAsync(tripId);
                _expenses = expenses;
                var accounts = (await _accountRepository.GetAllAsync()).ToDictionary(a => a.Id);
                var loadedCategories = await _trips.GetCategoriesAsync();
                var tripCategories = loadedCategories.ToDictionary(c => c.Id, c => c.Name);
                cancellationToken.ThrowIfCancellationRequested();

                _tripCategories = loadedCategories;
                Card = new TripCard(summary);
                Categories = summary.Categories.Select(c => new TripCategoryRow(c, summary.CurrencyCode)).ToList();
                Entries = expenses.Select(e => new TripEntryRow(
                        e.Description,
                        string.Join(" · ", new[]
                        {
                            DateFormatter.Format(e.Date, DateStyle.Medium),
                            e.TripCategoryId is { } id ? tripCategories.GetValueOrDefault(id, string.Empty) : string.Empty,
                            e.AccountId is { } accountId ? accounts.GetValueOrDefault(accountId)?.Name ?? string.Empty : string.Empty
                        }.Where(part => part.Length > 0)),
                        MoneyFormatter.Format(e.Amount, e.AccountId is { } a && accounts.TryGetValue(a, out var account) ? account.CurrencyCode : null)))
                    .ToList();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // A newer load replaced this one
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToLoadTrips);
            }
            finally
            {
                if (!cancellationToken.IsCancellationRequested)
                    IsLoading = false;
            }
        }

        // The trip's expenses, for the End trip sheet (which trip categories were used)
        private List<Transaction> _expenses = [];
        private List<TripCategory> _tripCategories = [];

        [ObservableProperty]
        private bool isEndSheetOpen;

        public ObservableCollection<TripEndRow> EndRows { get; } = [];

        /// <summary>The real expense categories an expense can move to (never "Travel").</summary>
        [ObservableProperty]
        private List<Category> endCategories = [];

        /// <summary>Picking one here sets every row to it.</summary>
        [ObservableProperty]
        private Category? sameForAll;

        partial void OnSameForAllChanged(Category? value)
        {
            if (value == null)
                return;
            foreach (var row in EndRows)
                row.Selected = value;
            // Cleared so picking the same category again applies it again after a row was changed by hand
            SameForAll = null;
        }

        [RelayCommand]
        private async Task OpenEndSheetAsync()
        {
            if (Card is not { IsActive: true })
                return;

            try
            {
                EndCategories = TripEndPlan.Targets(await _categoryRepository.GetByTypeAsync(CategoryType.Expense));
                EndRows.Clear();
                foreach (var row in TripEndPlan.Rows(_expenses, _tripCategories, EndCategories))
                    EndRows.Add(new TripEndRow(row.Source, EndCategories, row.Target));
                SameForAll = null;
                IsEndSheetOpen = true;
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToLoadTrips);
            }
        }

        [RelayCommand]
        private void CloseEndSheet() => IsEndSheetOpen = false;

        /// <summary>Closes the End trip sheet; false when it wasn't open.</summary>
        public bool CloseTopOverlay()
        {
            if (!IsEndSheetOpen)
                return false;

            CloseEndSheet();
            return true;
        }

        [RelayCommand]
        private async Task EndTripAsync()
        {
            if (Card == null)
                return;
            if (EndRows.Any(r => r.Selected == null))
            {
                await _errorHandler.HandleAsync(AppResources.PleaseChooseCategoryForEach);
                return;
            }

            try
            {
                await _trips.EndAsync(Card.TripId, EndRows.ToDictionary(r => r.Source.Id, r => r.Selected!.Id));
                CloseEndSheet();
                await LoadCommand.ExecuteAsync(null);
                await _dialogService.ToastAsync(AppResources.TripEndedToast);
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToEndTrip);
            }
        }

        [RelayCommand]
        private async Task ReopenAsync()
        {
            var trip = Card?.Trip;
            if (trip == null || trip.IsActive)
                return;

            if (await _trips.GetActiveAsync() != null)
            {
                await _dialogService.AlertAsync(AppResources.Error, AppResources.TripAlreadyActive);
                return;
            }

            if (!await _dialogService.ConfirmAsync(AppResources.ReopenTrip,
                    string.Format(AppResources.ReopenTripConfirmation, trip.Name), AppResources.ReopenTrip, AppResources.Cancel))
                return;

            try
            {
                await _trips.ReopenAsync(trip.Id);
                await LoadCommand.ExecuteAsync(null);
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToEndTrip);
            }
        }

        [RelayCommand]
        private async Task EditAsync()
        {
            if (Card != null)
                await _navigation.NavigateToAsync("TripsPage", new Dictionary<string, object> { ["EditTripId"] = Card.TripId.ToString() });
        }

        [RelayCommand]
        private async Task DeleteAsync()
        {
            var trip = Card?.Trip;
            if (trip == null)
                return;

            if (trip.IsActive && Card!.Summary.EntryCount > 0)
            {
                await _dialogService.AlertAsync(AppResources.CannotDelete, AppResources.TripDeleteNeedsEnd);
                return;
            }

            if (!await _dialogService.ConfirmAsync(AppResources.ConfirmDelete,
                    string.Format(AppResources.DeleteTripConfirmation, trip.Name), AppResources.YesDelete, AppResources.Cancel))
                return;

            try
            {
                await _trips.DeleteAsync(trip.Id);
                await _navigation.GoBackAsync();
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToDeleteTrip);
            }
        }
    }
}
