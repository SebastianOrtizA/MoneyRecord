using MoneyRecord.Helpers;
using System.Globalization;

namespace MoneyRecord.Converters
{
    /// <summary>
    /// Formats an amount in its account's currency: MultiBinding of (decimal amount, string currencyCode).
    /// An empty currency code means the base currency.
    /// </summary>
    public class CurrencyMoneyConverter : IMultiValueConverter
    {
        public object? Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
        {
            var amount = values.Length > 0 && values[0] is decimal d ? d : 0;
            var currencyCode = values.Length > 1 ? values[1] as string : null;
            return MoneyFormatter.Format(amount, currencyCode);
        }

        public object?[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
