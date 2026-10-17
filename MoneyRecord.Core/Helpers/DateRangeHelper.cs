using MoneyRecord.Models;

namespace MoneyRecord.Helpers
{
    /// <summary>
    /// Helper for date range calculations used across ViewModels.
    /// Centralizes date range logic to avoid duplication.
    /// </summary>
    public static class DateRangeHelper
    {
        /// <summary>
        /// Calculates the start and end dates based on the selected period type.
        /// </summary>
        /// <param name="periodType">The period type to calculate</param>
        /// <param name="customStartDate">Custom start date for custom period</param>
        /// <param name="customEndDate">Custom end date for custom period</param>
        /// <returns>A tuple containing the start and end dates</returns>
        public static (DateTime startDate, DateTime endDate) GetDateRange(
            PeriodType? periodType,
            DateTime customStartDate = default,
            DateTime customEndDate = default)
        {
            var now = DateTime.Now;
            DateTime startDate;
            DateTime endDate;

            switch (periodType)
            {
                case PeriodType.CalendarMonth:
                    startDate = new DateTime(now.Year, now.Month, 1);
                    endDate = startDate.AddMonths(1).AddTicks(-1);
                    break;
                case PeriodType.CalendarYear:
                    startDate = new DateTime(now.Year, 1, 1);
                    endDate = new DateTime(now.Year, 12, 31, 23, 59, 59, 999).AddTicks(9999);
                    break;
                case PeriodType.Today:
                    startDate = now.Date;
                    endDate = now.Date.AddDays(1).AddTicks(-1);
                    break;
                case PeriodType.LastWeek:
                    startDate = now.Date.AddDays(-7);
                    endDate = now.Date.AddDays(1).AddTicks(-1);
                    break;
                case PeriodType.LastYear:
                    startDate = now.Date.AddYears(-1);
                    endDate = now.Date.AddDays(1).AddTicks(-1);
                    break;
                case PeriodType.CustomPeriod:
                    startDate = customStartDate.Date;
                    endDate = customEndDate.Date.AddDays(1).AddTicks(-1);
                    break;
                default: // Last Month
                    startDate = now.Date.AddMonths(-1);
                    endDate = now.Date.AddDays(1).AddTicks(-1);
                    break;
            }

            return (startDate, endDate);
        }

        /// <summary>
        /// How many calendar months [<paramref name="start"/>, <paramref name="end"/>] covers when it is whole months (from
        /// a 1st to the last day of a month, any time of day); null otherwise. The one rule for "whole months", used by
        /// <see cref="GetPreviousRange"/> and the budget projection.
        /// </summary>
        public static int? WholeMonths(DateTime start, DateTime end)
        {
            if (start == default || end == default || end < start || start.Day != 1
                || end.Day != DateTime.DaysInMonth(end.Year, end.Month))
                return null;

            return (end.Year - start.Year) * 12 + end.Month - start.Month + 1;
        }

        /// <summary>
        /// The last <paramref name="months"/> calendar months up to <paramref name="today"/>'s, this one included: the window of
        /// the time-series reports (Trends, Cash flow, Net worth) and of their export.
        /// </summary>
        public static (DateTime startDate, DateTime endDate) GetLastMonths(int months, DateTime today)
        {
            var thisMonth = new DateTime(today.Year, today.Month, 1);
            return (thisMonth.AddMonths(-(months - 1)), thisMonth.AddMonths(1).AddTicks(-1));
        }

        /// <summary>
        /// The period just before [<paramref name="start"/>, <paramref name="end"/>]: whole calendar months move back by
        /// as many months (October → September, a calendar year → the year before), any other range by its own length.
        /// </summary>
        public static (DateTime startDate, DateTime endDate) GetPreviousRange(DateTime start, DateTime end)
        {
            if (WholeMonths(start, end) is { } months)
                return (start.AddMonths(-months), start.AddTicks(-1));

            var previousEnd = start.AddTicks(-1);
            return (previousEnd - (end - start), previousEnd);
        }

        /// <summary>
        /// The budget period containing <paramref name="now"/>: its day, calendar month or calendar year, up to the
        /// last tick of it.
        /// </summary>
        public static (DateTime startDate, DateTime endDate) GetBudgetWindow(BudgetPeriod period, DateTime now)
        {
            var start = period switch
            {
                BudgetPeriod.Day => now.Date,
                BudgetPeriod.Year => new DateTime(now.Year, 1, 1),
                _ => new DateTime(now.Year, now.Month, 1)
            };
            var next = period switch
            {
                BudgetPeriod.Day => start.AddDays(1),
                BudgetPeriod.Year => start.AddYears(1),
                _ => start.AddMonths(1)
            };

            return (start, next.AddTicks(-1));
        }
    }
}
