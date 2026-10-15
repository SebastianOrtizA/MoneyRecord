using MoneyRecord.Helpers;
using MoneyRecord.Models;
using System.Globalization;

namespace MoneyRecord.Converters
{
    /// <summary>
    /// A <see cref="TransactionGroup"/>'s totals (<see cref="TransactionGroup.TotalText"/>): signed by type, one per
    /// currency; transfer groups show the amount moved (neutral).
    /// </summary>
    public class GroupTotalConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is TransactionGroup group ? group.TotalText : MoneyFormatter.Format(0);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

    }
}
