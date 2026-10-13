using CommunityToolkit.Mvvm.ComponentModel;
using MoneyRecord.Helpers;
using MoneyRecord.Models;

namespace MoneyRecord.ViewModels
{
    /// <summary>
    /// The one period the app looks at (singleton): Home, Transactions, Budgets and the Spending / Income reports all show
    /// it, so changing it on one tab changes it everywhere. The time-series reports (Trends, Cash flow, Net worth) share
    /// <see cref="MonthCount"/> instead, since they need whole months.
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

        public List<PeriodItem> Periods { get; } = PeriodHelper.GetReportPeriods();

        public List<int> MonthCountOptions { get; } = [3, 6, 12];

        public PeriodViewModel()
        {
            selectedPeriod = Periods.First(p => p.Type == PeriodType.CalendarMonth);
        }

        /// <summary>The period changed (not <see cref="MonthCount"/>).</summary>
        public event Action? Changed;

        /// <summary><see cref="MonthCount"/> changed.</summary>
        public event Action? MonthCountChanged;

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
