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

    public class BoolToSortTextConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isAscending)
            {
                return isAscending ? "↑ Oldest First" : "↓ Newest First";
            }
            return "Sort";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class InvertedBoolConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            return true;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            return true;
        }
    }

    public class BoolToGroupTextConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isGrouped)
            {
                return isGrouped ? "📂 Grouped" : "📄 List View";
            }
            return "📄 List View";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
