using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using System.Globalization;

namespace MoneyRecord.Converters
{
    /// <summary>
    /// Converts BudgetPeriod to display name
    /// </summary>
    public class BudgetPeriodToDisplayConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is BudgetPeriod period)
            {
                return period switch
                {
                    BudgetPeriod.Day => AppResources.PerDay,
                    BudgetPeriod.Month => AppResources.PerMonth,
                    BudgetPeriod.Year => AppResources.PerYear,
                    _ => period.ToString()
                };
            }
            return string.Empty;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
