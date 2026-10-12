using MoneyRecord.Models;
using System.Globalization;

namespace MoneyRecord.Converters
{
    /// <summary>
    /// Converts TransactionType to appropriate color for group totals.
    /// </summary>
    public class GroupTypeToColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is TransactionGroup group)
            {
                if (group.Type == TransactionType.Transfer)
                {
                    // Orange for transfers
                    return Application.Current?.RequestedTheme == AppTheme.Dark
                        ? Color.FromArgb("#FFB74D")
                        : Color.FromArgb("#FF9800");
                }

                if (group.Type == TransactionType.Income)
                {
                    // Green for income
                    return Application.Current?.RequestedTheme == AppTheme.Dark
                        ? Color.FromArgb("#66BB6A")
                        : Color.FromArgb("#2E7D32");
                }
                else
                {
                    // Red for expense
                    return Application.Current?.RequestedTheme == AppTheme.Dark
                        ? Color.FromArgb("#EF5350")
                        : Color.FromArgb("#D32F2F");
                }
            }
            return Colors.Gray;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
