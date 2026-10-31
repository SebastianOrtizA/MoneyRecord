using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;

namespace MoneyRecord.Helpers
{
    public class PeriodItem
    {
        public PeriodType Type { get; set; }

        public string DisplayName => Type switch
        {
            PeriodType.CalendarMonth => AppResources.CalendarMonth,
            PeriodType.CalendarYear => AppResources.CalendarYear,
            PeriodType.Today => AppResources.Today,
            PeriodType.LastWeek => AppResources.LastWeek,
            PeriodType.LastMonth => AppResources.LastMonth,
            PeriodType.LastYear => AppResources.LastYear,
            PeriodType.CustomPeriod => AppResources.CustomPeriod,
            _ => Type.ToString()
        };

        public override string ToString() => DisplayName;

        public override bool Equals(object? obj)
        {
            if (obj is PeriodItem other)
                return Type == other.Type;
            return false;
        }

        public override int GetHashCode() => Type.GetHashCode();
    }

    /// <summary>
    /// Represents a budget period option (Day, Month, Year) for selection
    /// </summary>
    public class BudgetPeriodItem
    {
        public BudgetPeriod Period { get; set; }

        public string DisplayName => Period switch
        {
            BudgetPeriod.Day => AppResources.PerDay,
            BudgetPeriod.Month => AppResources.PerMonth,
            BudgetPeriod.Year => AppResources.PerYear,
            _ => Period.ToString()
        };

        public override string ToString() => DisplayName;

        public override bool Equals(object? obj)
        {
            if (obj is BudgetPeriodItem other)
                return Period == other.Period;
            return false;
        }

        public override int GetHashCode() => Period.GetHashCode();
    }

    /// <summary>One row of the period sheet.</summary>
    public record PeriodChoice(PeriodItem Item, bool IsSelected);

    /// <summary>What the period stepper moves by.</summary>
    public enum PeriodStep
    {
        Month,
        Year
    }

    public static class PeriodHelper
    {
        /// <summary>
        /// The step a range allows: <see cref="PeriodStep.Month"/> for exactly one calendar month,
        /// <see cref="PeriodStep.Year"/> for exactly one calendar year, null for any other range.
        /// </summary>
        public static PeriodStep? StepOf(DateTime start, DateTime end) =>
            DateRangeHelper.WholeMonths(start, end) switch
            {
                1 => PeriodStep.Month,
                12 when start.Month == 1 => PeriodStep.Year,
                _ => null
            };

        /// <summary>The month or year <paramref name="n"/> steps from the one starting at <paramref name="start"/>, whole, up to its last tick.</summary>
        public static (DateTime Start, DateTime End) Shift(DateTime start, PeriodStep step, int n)
        {
            var first = step == PeriodStep.Year
                ? new DateTime(start.Year + n, 1, 1)
                : new DateTime(start.Year, start.Month, 1).AddMonths(n);
            var next = step == PeriodStep.Year ? first.AddYears(1) : first.AddMonths(1);
            return (first, next.AddTicks(-1));
        }

        public static List<PeriodItem> GetReportPeriods() =>
        [
            new PeriodItem { Type = PeriodType.CalendarMonth },
            new PeriodItem { Type = PeriodType.CalendarYear },
            new PeriodItem { Type = PeriodType.Today },
            new PeriodItem { Type = PeriodType.LastWeek },
            new PeriodItem { Type = PeriodType.LastMonth },
            new PeriodItem { Type = PeriodType.LastYear },
            new PeriodItem { Type = PeriodType.CustomPeriod }
        ];
    }
}
