using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.ViewModels
{
    /// <summary>
    /// The one period the app looks at (singleton): Home, Transactions, Budgets and the Spending / Income reports all show
    /// it, so changing it on one tab changes it everywhere. The time-series reports (Trends, Cash flow, Net worth) share
    /// <see cref="MonthCount"/> instead, since they need whole months. Every report shows one currency,
    /// <see cref="ReportCurrency"/> (until exchange rates exist).
    /// </summary>
    public partial class PeriodViewModel : ObservableObject
    {
        // Set while ShowMonth changes three properties, so listeners reload once
        private bool _batching;

        [ObservableProperty]
        private PeriodItem selectedPeriod;

        [ObservableProperty]
        private DateTime customStartDate = DateTime.Today.AddMonths(-1);

        [ObservableProperty]
        private DateTime customEndDate = DateTime.Today;

        [ObservableProperty]
        private bool isCustomPeriodSelected;

        /// <summary>How many months the time-series reports cover (the last N, this one included).</summary>
        [ObservableProperty]
        private int monthCount = 6;

        [ObservableProperty]
        private List<PeriodItem> periods = PeriodHelper.GetReportPeriods();

        public List<int> MonthCountOptions { get; } = [3, 6, 12];

        // The reports' currency; null = the base currency, so a base currency change in Settings is followed
        private string? _reportCurrency;

        // Where the "Show in" choice is remembered (none in tests that don't need it)
        private readonly ICurrencyDisplaySettings? _display;

        /// <summary>The reports' currency chips; empty with a single currency (nothing to choose).</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasCurrencyChoices))]
        private List<CurrencyChoice> currencyChoices = [];

        public bool HasCurrencyChoices => CurrencyChoices.Count > 1;

        /// <summary>The currency every report shows (an ISO code; the base currency while converted).</summary>
        public string ReportCurrency => IsConverted ? MoneyFormatter.BaseCurrency : _reportCurrency ?? MoneyFormatter.BaseCurrency;

        /// <summary>The reports show every currency converted into the base (the "≈" chip).</summary>
        public bool IsConverted { get; private set; }

        public PeriodViewModel() : this(null)
        {
        }

        public PeriodViewModel(ICurrencyDisplaySettings? display)
        {
            _display = display;
            selectedPeriod = periods.First(p => p.Type == PeriodType.CalendarMonth);
            // Lives as long as the app (singleton): the pickers follow a language change
            LocalizationService.Instance.LanguageChanged += (_, _) => RefreshNames();
        }

        /// <summary>The period changed (not <see cref="MonthCount"/>).</summary>
        public event Action? Changed;

        /// <summary><see cref="MonthCount"/> changed.</summary>
        public event Action? MonthCountChanged;

        /// <summary><see cref="ReportCurrency"/> changed (a chip was tapped); only the reports follow it.</summary>
        public event Action? CurrencyChanged;

        /// <summary>
        /// Runs <paramref name="onChanged"/> on every period change until the result is disposed: a page listens only
        /// while it is shown (it reloads anyway when it appears), so a change never reloads the hidden tabs.
        /// </summary>
        public IDisposable WatchChanges(Action onChanged)
        {
            Changed += onChanged;
            return new Subscription(() => Changed -= onChanged);
        }

        /// <summary>Like <see cref="WatchChanges"/>, for <see cref="MonthCount"/>.</summary>
        public IDisposable WatchMonthCount(Action onChanged)
        {
            MonthCountChanged += onChanged;
            return new Subscription(() => MonthCountChanged -= onChanged);
        }

        /// <summary>Like <see cref="WatchChanges"/>, for <see cref="ReportCurrency"/>.</summary>
        public IDisposable WatchCurrency(Action onChanged)
        {
            CurrencyChanged += onChanged;
            return new Subscription(() => CurrencyChanged -= onChanged);
        }

        private sealed class Subscription(Action unsubscribe) : IDisposable
        {
            private Action? _unsubscribe = unsubscribe;

            public void Dispose()
            {
                _unsubscribe?.Invoke();
                _unsubscribe = null;
            }
        }

        /// <summary>
        /// A report is loading: the currencies there are now (<see cref="ReportCurrencies.InUse"/>), and whether totals are
        /// converted (<see cref="IConversionService.IsConvertedAsync"/>; the "≈" chip comes first when they can be). A
        /// chosen currency no account uses any more falls back to the base currency.
        /// </summary>
        public void SetCurrencies(IReadOnlyList<string> codes, bool canConvert = false, bool converted = false)
        {
            if (_reportCurrency != null && !codes.Contains(_reportCurrency))
                _reportCurrency = null;
            IsConverted = converted && canConvert && codes.Count > 1;

            List<CurrencyChoice> choices = codes.Count > 1
                ? codes.Select(c => new CurrencyChoice(c, !IsConverted && c == ReportCurrency)).ToList()
                : [];
            if (canConvert && codes.Count > 1)
                choices.Insert(0, new CurrencyChoice(MoneyFormatter.BaseCurrency, IsConverted, IsConverted: true));
            if (!choices.SequenceEqual(CurrencyChoices))
                CurrencyChoices = choices;
        }

        /// <summary>A chip was tapped: every report shows this currency, and the one on screen (watching) reloads.</summary>
        [RelayCommand]
        private void SelectCurrency(CurrencyChoice? choice)
        {
            if (choice == null || choice.IsSelected)
                return;

            IsConverted = choice.IsConverted;
            if (!choice.IsConverted)
                _reportCurrency = choice.Code == MoneyFormatter.BaseCurrency ? null : choice.Code;
            // The same choice as Home's switch
            if (_display != null)
                _display.ShowConverted = choice.IsConverted;

            CurrencyChoices = CurrencyChoices.Select(c => c with { IsSelected = c.Code == choice.Code && c.IsConverted == choice.IsConverted }).ToList();
            CurrencyChanged?.Invoke();
        }

        public (DateTime Start, DateTime End) Range =>
            DateRangeHelper.GetDateRange(SelectedPeriod?.Type, CustomStartDate, CustomEndDate);

        /// <summary>The period just before <see cref="Range"/>, for "vs previous period".</summary>
        public (DateTime Start, DateTime End) PreviousRange => DateRangeHelper.GetPreviousRange(Range.Start, Range.End);

        /// <summary>Shows one calendar month (a report drill-down into a month) as a custom period.</summary>
        public void ShowMonth(DateTime month)
        {
            var first = new DateTime(month.Year, month.Month, 1);
            _batching = true;
            try
            {
                CustomStartDate = first;
                CustomEndDate = first.AddMonths(1).AddDays(-1);
                SelectedPeriod = Periods.First(p => p.Type == PeriodType.CustomPeriod);
            }
            finally
            {
                _batching = false;
            }

            Changed?.Invoke();
        }

        /// <summary>
        /// The language changed: new items, so the pickers show the translated names. The period itself stays, so
        /// nothing reloads.
        /// </summary>
        public void RefreshNames()
        {
            var type = SelectedPeriod?.Type ?? PeriodType.CalendarMonth;
            _batching = true;
            try
            {
                Periods = PeriodHelper.GetReportPeriods();
                SelectedPeriod = Periods.First(p => p.Type == type);
            }
            finally
            {
                _batching = false;
            }
        }

        partial void OnSelectedPeriodChanged(PeriodItem value)
        {
            IsCustomPeriodSelected = value?.Type == PeriodType.CustomPeriod;
            RaiseChanged();
        }

        partial void OnCustomStartDateChanged(DateTime value)
        {
            if (IsCustomPeriodSelected)
                RaiseChanged();
        }

        partial void OnCustomEndDateChanged(DateTime value)
        {
            if (IsCustomPeriodSelected)
                RaiseChanged();
        }

        partial void OnMonthCountChanged(int value) => MonthCountChanged?.Invoke();

        private void RaiseChanged()
        {
            if (!_batching)
                Changed?.Invoke();
        }
    }
}
