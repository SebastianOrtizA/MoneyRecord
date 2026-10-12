using MoneyRecord.Helpers;
using MoneyRecord.Models;
using System.Globalization;

namespace MoneyRecord.Converters
{
    /// <summary>
    /// Formats a <see cref="LedgerEntry"/>'s signed amount in its currency: negative for expenses and outgoing transfers.
    /// </summary>
    public class TransactionAmountConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is LedgerEntry entry
                ? MoneyFormatter.Format(entry.SignedAmount, entry.CurrencyCode)
                : MoneyFormatter.Format(0);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

    }
}
