using MoneyRecord.Models;
using System.Globalization;

namespace MoneyRecord.Converters
{
    /// <summary>
    /// Converts TransactionType to appropriate color for group totals.
    /// </summary>
    public class GroupTypeToColorConverter : IValueConverter
    {
        private static readonly TransactionTypeToColorConverter Inner = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is TransactionGroup group ? Inner.Convert(group.Type, targetType, parameter, culture) : Colors.Gray;

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
