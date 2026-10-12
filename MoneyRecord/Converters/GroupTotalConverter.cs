using MoneyRecord.Helpers;
using MoneyRecord.Models;
using System.Globalization;

namespace MoneyRecord.Converters
{
    /// <summary>
    /// Converts group total to display format with proper sign based on transaction type.
    /// For transfer groups, shows the total amount moved (neutral).
    /// Requires TransactionGroup object as binding source.
    /// </summary>
    public class GroupTotalConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is TransactionGroup group)
            {
                // For transfer groups, show neutral amount (no sign)
                if (group.Type == TransactionType.Transfer)
                {
                    return MoneyFormatter.Format(Math.Abs(group.Total), group.CurrencyCode);
                }

                // For expense groups, display as negative; for income groups, display as positive
                var displayAmount = group.Type == TransactionType.Expense
                    ? -Math.Abs(group.Total)
                    : Math.Abs(group.Total);
                return MoneyFormatter.Format(displayAmount, group.CurrencyCode);
            }
            return MoneyFormatter.Format(0);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

    }
}
