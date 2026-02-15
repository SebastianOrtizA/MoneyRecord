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
    }
}
