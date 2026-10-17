using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.ViewModels
{
    /// <summary>One trip: spent against its budget, per trip category, and its expenses.</summary>
    [QueryProperty(nameof(TripIdText), "TripId")]
    public partial class TripViewModel : ObservableObject
    {
        private readonly ITripService _trips;
        private readonly IAccountRepository _accountRepository;
        private readonly INavigationService _navigation;
        private readonly IErrorHandler _errorHandler;
        private readonly IDialogService _dialogService;

        public string? TripIdText { get; set; }

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasTrip), nameof(Title))]
        private TripCard? card;

        public bool HasTrip => Card != null;

        public string Title => Card?.Name ?? AppResources.TripsTitle;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasCategories))]
        private List<TripCategoryRow> categories = [];

        public bool HasCategories => Categories.Count > 0;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasEntries))]
        private List<TripEntryRow> entries = [];

        public bool HasEntries => Entries.Count > 0;

        public TripViewModel(ITripService trips, IAccountRepository accountRepository, INavigationService navigation,
            IErrorHandler errorHandler, IDialogService dialogService)
        {
            _trips = trips;
            _accountRepository = accountRepository;
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
                var accounts = (await _accountRepository.GetAllAsync()).ToDictionary(a => a.Id);
                var tripCategories = (await _trips.GetCategoriesAsync()).ToDictionary(c => c.Id, c => c.Name);
                cancellationToken.ThrowIfCancellationRequested();

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
