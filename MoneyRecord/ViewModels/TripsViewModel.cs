using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.ViewModels
{
    /// <summary>One trip category's limit in the trip sheet; empty = no limit.</summary>
    public partial class TripLimitRow(TripCategory category, string limit) : ObservableObject
    {
        public TripCategory Category { get; } = category;

        [ObservableProperty]
        private string limit = limit;
    }

    /// <summary>Trips page: the active trip first, then the history (a tap opens the trip), and the add/edit sheet.</summary>
    [QueryProperty(nameof(EditTripId), "EditTripId")]
    public partial class TripsViewModel : ObservableObject
    {
        private readonly ITripService _trips;
        private readonly IAccountRepository _accountRepository;
        private readonly INavigationService _navigation;
        private readonly IErrorHandler _errorHandler;
        private readonly IDialogService _dialogService;
        private readonly TimeProvider _clock;

        private List<TripCategory> _tripCategories = [];

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasTrips))]
        private List<TripCard> cards = [];

        public bool HasTrips => Cards.Count > 0;

        /// <summary>A trip to edit, from the trip page's Edit button: its sheet opens once loaded.</summary>
        public string? EditTripId { get; set; }

        [ObservableProperty]
        private bool isFormOpen;

        /// <summary>The trip the sheet edits; null when it starts one.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsEditMode), nameof(FormTitle), nameof(SaveText))]
        private Trip? editingTrip;

        public bool IsEditMode => EditingTrip != null;

        public string FormTitle => IsEditMode ? AppResources.EditTrip : AppResources.StartTrip;

        public string SaveText => IsEditMode ? AppResources.Save : AppResources.StartTrip;

        [ObservableProperty]
        private string formName = string.Empty;

        [ObservableProperty]
        private DateTime formStartDate = DateTime.Today;

        [ObservableProperty]
        private bool hasEndDate;

        [ObservableProperty]
        private DateTime formEndDate = DateTime.Today.AddDays(7);

        [ObservableProperty]
        private string formCurrency = string.Empty;

        [ObservableProperty]
        private List<string> currencies = [];

        [ObservableProperty]
        private string formBudget = string.Empty;

        /// <summary>"None" first, then the user's accounts.</summary>
        [ObservableProperty]
        private List<Account> accountChoices = [];

        [ObservableProperty]
        private Account? selectedAccount;

        public ObservableCollection<TripLimitRow> LimitRows { get; } = [];

        public TripsViewModel(ITripService trips, IAccountRepository accountRepository, INavigationService navigation,
            IErrorHandler errorHandler, IDialogService dialogService, TimeProvider clock)
        {
            _trips = trips;
            _accountRepository = accountRepository;
            _navigation = navigation;
            _errorHandler = errorHandler;
            _dialogService = dialogService;
            _clock = clock;
        }

        public async Task InitializeAsync()
        {
            await LoadTripsCommand.ExecuteAsync(null);
            if (int.TryParse(EditTripId, out var id))
            {
                EditTripId = null;
                var trip = Cards.FirstOrDefault(c => c.TripId == id)?.Trip;
                if (trip != null)
                    await OpenFormAsync(trip);
            }
        }

        [RelayCommand(AllowConcurrentExecutions = true)]
        private async Task LoadTripsAsync(CancellationToken cancellationToken)
        {
            try
            {
                IsLoading = true;
                var trips = await _trips.GetAllAsync();
                var summaries = new List<TripCard>();
                foreach (var trip in trips)
                    summaries.Add(new TripCard(await _trips.GetSummaryAsync(trip.Id)));
                cancellationToken.ThrowIfCancellationRequested();
                Cards = summaries;
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
        private async Task OpenTripAsync(TripCard? card)
        {
            if (card != null)
                await _navigation.NavigateToAsync("TripPage", new Dictionary<string, object> { ["TripId"] = card.TripId.ToString() });
        }

        [RelayCommand]
        private Task OpenCategoriesAsync() => _navigation.NavigateToAsync("TripCategoriesPage");

        [RelayCommand]
        private async Task AddTripAsync()
        {
            if (Cards.Any(c => c.IsActive))
            {
                await _dialogService.AlertAsync(AppResources.Error, AppResources.TripAlreadyActive);
                return;
            }

            await OpenFormAsync(null);
        }

        private async Task OpenFormAsync(Trip? trip)
        {
            try
            {
                _tripCategories = await _trips.GetCategoriesAsync();
                var accounts = await _accountRepository.GetOwnAsync();
                AccountChoices = [new Account { Id = 0, Name = AppResources.TripNoAccount }, .. accounts];
                var baseCode = MoneyFormatter.BaseCurrency;
                Currencies = accounts.Select(a => MoneyFormatter.ResolveCurrencyCode(a.CurrencyCode)).Append(baseCode)
                    .Distinct().OrderBy(code => code != baseCode).ThenBy(code => code, StringComparer.Ordinal).ToList();

                EditingTrip = trip;
                var limits = trip == null ? [] : await _trips.GetLimitsAsync(trip.Id);
                FormName = trip?.Name ?? string.Empty;
                FormStartDate = trip?.StartDate ?? _clock.GetLocalNow().Date;
                HasEndDate = trip?.EndDate != null;
                FormEndDate = trip?.EndDate ?? FormStartDate.AddDays(7);
                FormCurrency = trip == null ? baseCode : MoneyFormatter.ResolveCurrencyCode(trip.CurrencyCode);
                FormBudget = trip?.Budget is { } budget ? MoneyFormatter.FormatForEntry(budget, FormCurrency) : string.Empty;
                SelectedAccount = AccountChoices.FirstOrDefault(a => a.Id == (trip?.AccountId ?? 0)) ?? AccountChoices[0];
                LimitRows.Clear();
                foreach (var category in _tripCategories)
                    LimitRows.Add(new TripLimitRow(category,
                        limits.TryGetValue(category.Id, out var limit) ? MoneyFormatter.FormatForEntry(limit, FormCurrency) : string.Empty));
                IsFormOpen = true;
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToLoadTrips);
            }
        }

        [RelayCommand]
        private void CloseForm()
        {
            IsFormOpen = false;
            EditingTrip = null;
        }

        /// <summary>Closes the sheet; false when it wasn't open.</summary>
        public bool CloseTopOverlay()
        {
            if (!IsFormOpen)
                return false;

            CloseForm();
            return true;
        }

        [RelayCommand]
        private async Task SaveTripAsync()
        {
            var name = FormName.Trim();
            if (name.Length == 0)
            {
                await _errorHandler.HandleAsync(AppResources.PleaseEnterTripName);
                return;
            }

            decimal? budget = null;
            if (!string.IsNullOrWhiteSpace(FormBudget))
            {
                budget = AmountParser.Parse(FormBudget, FormCurrency);
                if (budget <= 0 || !Money.IsInRange(budget.Value))
                {
                    await _errorHandler.HandleAsync(AppResources.PleaseEnterValidAmount);
                    return;
                }
            }

            var limits = new Dictionary<int, decimal>();
            foreach (var row in LimitRows.Where(r => !string.IsNullOrWhiteSpace(r.Limit)))
            {
                var limit = AmountParser.Parse(row.Limit, FormCurrency);
                if (limit <= 0 || !Money.IsInRange(limit))
                {
                    await _errorHandler.HandleAsync(AppResources.PleaseEnterValidAmount);
                    return;
                }

                limits[row.Category.Id] = limit;
            }

            if (HasEndDate && FormEndDate.Date < FormStartDate.Date)
            {
                await _errorHandler.HandleAsync(AppResources.TripEndBeforeStart);
                return;
            }

            var adding = !IsEditMode;
            var trip = EditingTrip ?? new Trip();
            trip.Name = name;
            trip.StartDate = FormStartDate.Date;
            trip.EndDate = HasEndDate ? FormEndDate.Date : null;
            trip.CurrencyCode = FormCurrency;
            trip.Budget = budget;
            trip.AccountId = SelectedAccount is { Id: > 0 } account ? account.Id : null;
            try
            {
                await _trips.SaveAsync(trip, limits);
                CloseForm();
                await LoadTripsCommand.ExecuteAsync(null);
                if (adding)
                    await _dialogService.ToastAsync(AppResources.TripStarted);
            }
            catch (InvalidOperationException) when (adding)
            {
                await _dialogService.AlertAsync(AppResources.Error, AppResources.TripAlreadyActive);
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToSaveTrip);
            }
        }
    }
}
