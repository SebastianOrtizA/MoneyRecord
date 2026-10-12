using MoneyRecord.Helpers;
using System.Globalization;

namespace MoneyRecord.Converters
{
    /// <summary>
    /// Like <see cref="MoneyConverter"/> with a plus sign on a positive amount (+$50.00, -$50.00), for net amounts where
    /// the sign carries the meaning, not only the colour.
    /// </summary>
    public class SignedMoneyConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return MoneyFormatter.FormatSigned(value is decimal amount ? amount : 0);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
