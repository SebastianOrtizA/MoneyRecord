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
            var endDate = DateTime.Now.Date.AddDays(1).AddTicks(-1);
            DateTime startDate;

            switch (periodType)
            {
                case PeriodType.Today:
                    startDate = DateTime.Now.Date;
                    break;
                case PeriodType.LastWeek:
                    startDate = DateTime.Now.Date.AddDays(-7);
                    break;
                case PeriodType.LastYear:
                    startDate = DateTime.Now.Date.AddYears(-1);
                    break;
                case PeriodType.CustomPeriod:
                    startDate = customStartDate.Date;
                    endDate = customEndDate.Date.AddDays(1).AddTicks(-1);
                    break;
                default: // Last Month
                    startDate = DateTime.Now.Date.AddMonths(-1);
                    break;
            }

            return (startDate, endDate);
        }
    }
}
