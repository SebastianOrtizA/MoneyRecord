using MoneyRecord.Helpers;
using System.Globalization;

namespace MoneyRecord.Converters
{
    /// <summary>
    /// Formats a decimal amount as money in the base currency, with the sign before the symbol (-$50.00).
    /// For totals and other amounts that belong to no single account.
    /// </summary>
    public class MoneyConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return MoneyFormatter.Format(value is decimal amount ? amount : 0);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
