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
            if (value is Models.GroupingMode mode)
            {
                return mode switch
                {
                    Models.GroupingMode.None => "📄 List View",
                    Models.GroupingMode.Category => "📂 By Category",
                    Models.GroupingMode.Account => "🏦 By Account",
                    _ => "📄 List View"
                };
            }
            // Backward compatibility with bool
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

    public class BoolToExpandIconConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isExpanded)
            {
                return isExpanded ? "▼" : "▶";
            }
            return "▶";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class GroupingModeToIsGroupedConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is Models.GroupingMode mode)
            {
                return mode != Models.GroupingMode.None;
            }
            return false;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class GroupingModeToIsNotGroupedConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is Models.GroupingMode mode)
            {
                return mode == Models.GroupingMode.None;
            }
            return true;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BalanceToColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is decimal balance)
            {
                if (balance >= 0)
                {
                    // Green for positive balance
                    return Application.Current?.RequestedTheme == AppTheme.Dark
                        ? Color.FromArgb("#66BB6A")
                        : Color.FromArgb("#2E7D32");
                }
                else
                {
                    // Red for negative balance
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

    /// <summary>
    /// Converts transaction amount to display format: negative for expenses, positive for incomes.
    /// Requires Transaction object as binding source.
    /// </summary>
    public class TransactionAmountConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is Transaction transaction)
            {
                var displayAmount = transaction.Type == TransactionType.Expense
                    ? -Math.Abs(transaction.Amount)
                    : Math.Abs(transaction.Amount);
                return displayAmount.ToString("C2", culture);
            }
            return "$0.00";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts group total to display format with proper sign based on transaction type.
    /// Requires TransactionGroup object as binding source.
    /// </summary>
    public class GroupTotalConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is TransactionGroup group)
            {
                // For expense groups, display as negative; for income groups, display as positive
                var displayAmount = group.Type == TransactionType.Expense
                    ? -Math.Abs(group.Total)
                    : Math.Abs(group.Total);
                return displayAmount.ToString("C2", culture);
            }
            return "$0.00";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts TransactionType to appropriate color for group totals.
    /// </summary>
    public class GroupTypeToColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is TransactionGroup group)
            {
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
