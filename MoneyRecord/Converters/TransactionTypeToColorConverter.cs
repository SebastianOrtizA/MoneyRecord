using MoneyRecord.Models;
using System.Globalization;

namespace MoneyRecord.Converters
{
    public class TransactionTypeToColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is TransactionType type)
            {
                if (type == TransactionType.Income)
                {
                    // Green for income
                    return Application.Current?.RequestedTheme == AppTheme.Dark
                        ? Color.FromArgb("#66BB6A") // Light green for dark theme
                        : Color.FromArgb("#2E7D32"); // Dark green for light theme
                }
                else if (type == TransactionType.Transfer)
                {
                    // Orange for transfer
                    return Application.Current?.RequestedTheme == AppTheme.Dark
                        ? Color.FromArgb("#FFB74D") // Light orange for dark theme
                        : Color.FromArgb("#FF9800"); // Orange for light theme
                }
                else
                {
                    // Red for expense
                    return Application.Current?.RequestedTheme == AppTheme.Dark
                        ? Color.FromArgb("#EF5350") // Light red for dark theme
                        : Color.FromArgb("#D32F2F"); // Dark red for light theme
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
