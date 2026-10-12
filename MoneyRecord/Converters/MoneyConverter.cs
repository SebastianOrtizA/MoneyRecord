using MoneyRecord.Helpers;
using System.Globalization;

namespace MoneyRecord.Converters
{
    /// <summary>
    /// Formats a decimal amount as money in the base currency, with the sign before the symbol (-$50.00).
    /// For totals and other amounts that belong to no single account. ConverterParameter=Signed adds a plus sign to a
    /// positive amount (+$50.00), for net amounts.
    /// </summary>
    public class MoneyConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var amount = value is decimal d ? d : 0;
            return parameter as string == "Signed" ? MoneyFormatter.FormatSigned(amount) : MoneyFormatter.Format(amount);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
