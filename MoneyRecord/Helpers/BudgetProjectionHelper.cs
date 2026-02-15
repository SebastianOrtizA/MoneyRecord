using MoneyRecord.Models;

namespace MoneyRecord.Helpers
{
    /// <summary>
    /// Helper class to calculate projected budget limits based on period type and selected date range.
    /// </summary>
    public static class BudgetProjectionHelper
    {
        /// <summary>
        /// Calculates the projected budget limit based on the budget period and the selected date range.
        /// </summary>
        /// <param name="originalLimit">The original limit amount from the budget</param>
        /// <param name="budgetPeriod">The period type of the budget (Day, Month, Year)</param>
        /// <param name="selectedPeriodType">The selected period type in Budget mode</param>
        /// <param name="customStartDate">Start date for custom period</param>
        /// <param name="customEndDate">End date for custom period</param>
        /// <returns>The projected limit amount for the selected period</returns>
        public static decimal CalculateProjectedLimit(
            decimal originalLimit,
            BudgetPeriod budgetPeriod,
            PeriodType selectedPeriodType,
            DateTime customStartDate = default,
            DateTime customEndDate = default)
        {
            var now = DateTime.Now;

            return budgetPeriod switch
            {
                BudgetPeriod.Day => CalculateDayBudgetProjection(originalLimit, selectedPeriodType, now, customStartDate, customEndDate),
                BudgetPeriod.Month => CalculateMonthBudgetProjection(originalLimit, selectedPeriodType, now, customStartDate, customEndDate),
                BudgetPeriod.Year => CalculateYearBudgetProjection(originalLimit, selectedPeriodType, now, customStartDate, customEndDate),
                _ => originalLimit
            };
        }

        /// <summary>
        /// Calculates projection when budget period is set to Day
        /// </summary>
        private static decimal CalculateDayBudgetProjection(
            decimal limit,
            PeriodType periodType,
            DateTime now,
            DateTime customStartDate,
            DateTime customEndDate)
        {
            return periodType switch
            {
                // Current month: limit * current month total days
                PeriodType.CalendarMonth => limit * DateTime.DaysInMonth(now.Year, now.Month),
                
                // Current year: limit * current year total days
                PeriodType.CalendarYear => limit * GetDaysInYear(now.Year),
                
                // Today: limit is just the limit
                PeriodType.Today => limit,
                
                // Last week: limit * 7
                PeriodType.LastWeek => limit * 7,
                
                // Last month: limit * 30
                PeriodType.LastMonth => limit * 30,
                
                // Last year: limit * 365
                PeriodType.LastYear => limit * 365,
                
                // Custom period: limit * (total days in custom period)
                PeriodType.CustomPeriod => limit * GetCustomPeriodDays(customStartDate, customEndDate),
                
                _ => limit
            };
        }

        /// <summary>
        /// Calculates projection when budget period is set to Month
        /// </summary>
        private static decimal CalculateMonthBudgetProjection(
            decimal limit,
            PeriodType periodType,
            DateTime now,
            DateTime customStartDate,
            DateTime customEndDate)
        {
            return periodType switch
            {
                // Current month: limit is just the limit
                PeriodType.CalendarMonth => limit,
                
                // Current year: limit * 12
                PeriodType.CalendarYear => limit * 12,
                
                // Today: limit / current month total days
                PeriodType.Today => limit / DateTime.DaysInMonth(now.Year, now.Month),
                
                // Last week: limit / 30 * 7
                PeriodType.LastWeek => limit / 30 * 7,
                
                // Last month: limit is just the limit
                PeriodType.LastMonth => limit,
                
                // Last year: limit * 12
                PeriodType.LastYear => limit * 12,
                
                // Custom period: limit / 30 * (total days in custom period)
                PeriodType.CustomPeriod => limit / 30 * GetCustomPeriodDays(customStartDate, customEndDate),
                
                _ => limit
            };
        }

        /// <summary>
        /// Calculates projection when budget period is set to Year
        /// </summary>
        private static decimal CalculateYearBudgetProjection(
            decimal limit,
            PeriodType periodType,
            DateTime now,
            DateTime customStartDate,
            DateTime customEndDate)
        {
            return periodType switch
            {
                // Current month: limit / 12
                PeriodType.CalendarMonth => limit / 12,
                
                // Current year: limit is just the limit
                PeriodType.CalendarYear => limit,
                
                // Today: limit / current year total days
                PeriodType.Today => limit / GetDaysInYear(now.Year),
                
                // Last week: limit / 365 * 7
                PeriodType.LastWeek => limit / 365 * 7,
                
                // Last month: limit / 12
                PeriodType.LastMonth => limit / 12,
                
                // Last year: limit is just the limit
                PeriodType.LastYear => limit,
                
                // Custom period: limit / 365 * (total days in custom period)
                PeriodType.CustomPeriod => limit / 365 * GetCustomPeriodDays(customStartDate, customEndDate),
                
                _ => limit
            };
        }

        /// <summary>
        /// Gets the number of days in a year (accounts for leap years)
        /// </summary>
        private static int GetDaysInYear(int year)
        {
            return DateTime.IsLeapYear(year) ? 366 : 365;
        }

        /// <summary>
        /// Gets the number of days in a custom period
        /// </summary>
        private static int GetCustomPeriodDays(DateTime startDate, DateTime endDate)
        {
            if (startDate == default || endDate == default)
                return 1;
            
            var days = (endDate.Date - startDate.Date).Days + 1; // +1 to include both start and end dates
            return Math.Max(days, 1); // Ensure at least 1 day
        }
    }
}
