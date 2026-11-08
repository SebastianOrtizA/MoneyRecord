using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.ViewModels
{
    /// <summary>
    /// The Forecast page: the balance day by day for 30 / 60 / 90 days, for every account or one, in the shared report currency
    /// (or everything converted), and the events behind the line. The chart itself is drawn by the app's subclass
    /// (<see cref="OnViewBuilt"/>); everything here is plain data.
    /// </summary>
    public partial class ForecastViewModel : ObservableObject
    {
        private readonly IForecastService _forecast;
        private readonly IConversionService _conversion;
        private readonly IErrorHandler _errorHandler;
        private readonly INavigationService _navigationService;
        private IDisposable? _currencyWatch;

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private int days = 30;

        [ObservableProperty]
        private bool hasData;

        [ObservableProperty]
        private List<ForecastAccountOption> accountOptions = [];

        [ObservableProperty]
        private ForecastAccountOption? selectedAccount;

        /// <summary>The account picker is offered with several accounts in the shown currency; hidden while converted.</summary>
        public bool HasAccountChoices => AccountOptions.Count > 2;

        [ObservableProperty]
        private ForecastView? view;

        [ObservableProperty]
        private string lowestText = string.Empty;

        [ObservableProperty]
        private string endText = string.Empty;

        /// <summary>"No exchange rate yet: ¥1,000" while converted, for what had no rate; empty otherwise.</summary>
        [ObservableProperty]
        private string notConvertedText = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasNoEvents))]
        private List<ForecastEventRow> events = [];

        public bool HasNoEvents => Events.Count == 0;

        public ForecastViewModel(IForecastService forecast, PeriodViewModel period, IConversionService conversion,
            IErrorHandler errorHandler, INavigationService navigationService)
        {
            _forecast = forecast;
            Period = period;
            _conversion = conversion;
            _errorHandler = errorHandler;
            _navigationService = navigationService;
        }

        /// <summary>The shared period: the forecast follows its report currency chips.</summary>
        public PeriodViewModel Period { get; }

        /// <summary>The 30 / 60 / 90 day chips.</summary>
        public List<ForecastDayChoice> DayChoices => new[] { 30, 60, 90 }.Select(d => new ForecastDayChoice(d, d == Days)).ToList();

        [RelayCommand]
        private void SelectDays(ForecastDayChoice? choice)
        {
            if (choice != null)
                Days = choice.Days;
        }

        partial void OnDaysChanged(int value)
        {
            OnPropertyChanged(nameof(DayChoices));
            LoadCommand.Execute(null);
        }

        partial void OnSelectedAccountChanged(ForecastAccountOption? value)
        {
            // Picking an account re-draws from the same forecast; only a reload of the options sets this to null
            if (value != null && !_loadingOptions)
                LoadCommand.Execute(null);
        }

        private bool _loadingOptions;

        /// <summary>The page appeared: load, and follow the shared report currency until <see cref="Deactivate"/>.</summary>
        public Task InitializeAsync()
        {
            _currencyWatch ??= Period.WatchCurrency(() => LoadCommand.Execute(null));
            return LoadCommand.ExecuteAsync(null);
        }

        public void Deactivate()
        {
            _currencyWatch?.Dispose();
            _currencyWatch = null;
        }

        /// <summary>The chart is drawn from <paramref name="newView"/> (the app's subclass sets its series here).</summary>
        protected virtual void OnViewBuilt(ForecastView newView)
        {
        }

        /// <summary>Each run cancels the one in progress, so an older choice never overwrites a newer one.</summary>
        [RelayCommand(AllowConcurrentExecutions = true)]
        private async Task LoadAsync(CancellationToken cancellationToken)
        {
            try
            {
                IsLoading = true;
                var result = await _forecast.GetForecastAsync(Days, cancellationToken);
                var state = await _conversion.GetStateAsync();
                cancellationToken.ThrowIfCancellationRequested();

                Show(result, state, SelectedAccount?.AccountId);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // A newer load replaced this one
            }
            catch (Exception ex)
            {
                await _errorHandler.HandleAsync(ex, AppResources.FailedToLoadReportData);
            }
            finally
            {
                // The newer load owns the spinner
                if (!cancellationToken.IsCancellationRequested)
                    IsLoading = false;
            }
        }

        /// <summary>Puts <paramref name="result"/> on the page for the chosen currency and account.</summary>
        internal void Show(ForecastResult result, ConversionState state, int? accountId)
        {
            Period.SetCurrencies(result.Currencies.Select(c => c.CurrencyCode).ToList(), state);
            var converted = Period.IsConverted && result.Converted != null;
            var code = converted ? result.Converted!.CurrencyCode : Period.ReportCurrency;

            // Accounts of the shown currency (none to pick from while converted: the line is everything)
            var shownAccounts = converted
                ? []
                : result.Accounts.Where(a => string.Equals(a.CurrencyCode, code, StringComparison.OrdinalIgnoreCase)).ToList();
            var options = new List<ForecastAccountOption> { new(null, AppResources.AllAccounts) };
            options.AddRange(shownAccounts.Select(a => new ForecastAccountOption(a.AccountId, a.AccountName)));
            var chosen = options.FirstOrDefault(o => o.AccountId == accountId && accountId != null) ?? options[0];

            _loadingOptions = true;
            AccountOptions = options;
            SelectedAccount = chosen;
            _loadingOptions = false;
            OnPropertyChanged(nameof(HasAccountChoices));

            ForecastSeries? series;
            if (converted)
                series = result.Converted!.Series;
            else if (chosen.AccountId is { } id)
                series = shownAccounts.FirstOrDefault(a => a.AccountId == id)?.Series;
            else
                series = result.Currencies.FirstOrDefault(c => string.Equals(c.CurrencyCode, code, StringComparison.OrdinalIgnoreCase))?.Series;

            var accountById = result.Accounts.ToDictionary(a => a.AccountId);
            Events = result.Events
                .Where(e => accountById.TryGetValue(e.AccountId, out var a)
                    && (converted || string.Equals(a.CurrencyCode, code, StringComparison.OrdinalIgnoreCase))
                    && (chosen.AccountId == null || chosen.AccountId == e.AccountId))
                .Select(e => new ForecastEventRow(e, accountById[e.AccountId].AccountName, accountById[e.AccountId].CurrencyCode))
                .ToList();

            NotConvertedText = converted && result.NotConverted.Count > 0
                ? string.Format(AppResources.NotConvertedNote, CurrencyAmount.Format(result.NotConverted, separator: " + "))
                : string.Empty;

            HasData = series != null;
            if (series == null)
            {
                View = null;
                LowestText = EndText = string.Empty;
                return;
            }

            var built = new ForecastView(series.Points.Select(p => p.Date).ToList(),
                series.Points.Select(p => Money.FromCents(p.Cents)).ToList(), code, converted);
            View = built;
            LowestText = built.LowestText;
            EndText = built.EndText;
            OnViewBuilt(built);
        }

        /// <summary>A recurring event opens the recurring transactions, where its rule is edited.</summary>
        [RelayCommand]
        private Task OpenEventAsync(ForecastEventRow? row) =>
            row is { CanOpen: true } ? _navigationService.NavigateToAsync("RecurringTransactionsPage") : Task.CompletedTask;
    }
}
