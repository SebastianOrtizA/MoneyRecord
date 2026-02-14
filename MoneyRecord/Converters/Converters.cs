using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
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

    public class BoolToSortTextConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isAscending)
            {
                return isAscending ? AppResources.SortOldestFirst : AppResources.SortNewestFirst;
            }
            return AppResources.Sort;
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
                    Models.GroupingMode.None => AppResources.ListView,
                    Models.GroupingMode.Category => AppResources.GroupByCategory,
                    Models.GroupingMode.Account => AppResources.GroupByAccount,
                    _ => AppResources.ListView
                };
            }
            // Backward compatibility with bool
            if (value is bool isGrouped)
            {
                return isGrouped ? AppResources.Grouped : AppResources.ListView;
            }
            return AppResources.ListView;
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
                // Material Design Icons: chevron-down (F0140) and chevron-right (F0142)
                return isExpanded ? "\U000F0140" : "\U000F0142";
            }
            return "\U000F0142";
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
    /// For transfers: negative for outgoing, positive for incoming.
    /// Requires Transaction object as binding source.
    /// </summary>
    public class TransactionAmountConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is Transaction transaction)
            {
                decimal displayAmount;
                
                if (transaction.Type == TransactionType.Transfer)
                {
                    // For transfers: negative if outgoing, positive if incoming
                    displayAmount = transaction.IsOutgoingTransfer
                        ? -Math.Abs(transaction.Amount)
                        : Math.Abs(transaction.Amount);
                }
                else
                {
                    displayAmount = transaction.Type == TransactionType.Expense
                        ? -Math.Abs(transaction.Amount)
                        : Math.Abs(transaction.Amount);
                }
                
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
                // Check both emoji version (legacy) and text version
                if (group.GroupName == "🔄 Transfers" || group.GroupName == "Transfers" || group.Type == TransactionType.Transfer)
                {
                    return Math.Abs(group.Total).ToString("C2", culture);
                }

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
                // Check if this is a transfer group by name or type
                // Support both emoji version (legacy) and text version
                if (group.GroupName == "🔄 Transfers" || group.GroupName == "Transfers" || group.Type == TransactionType.Transfer)
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
                else if (group.Type == TransactionType.Transfer)
                {
                    // Orange for transfers
                    return Application.Current?.RequestedTheme == AppTheme.Dark
                        ? Color.FromArgb("#FFB74D")
                        : Color.FromArgb("#FF9800");
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

    /// <summary>
    /// Converts a string to a boolean. Returns true if the string is not null or empty.
    /// </summary>
    public class StringToBoolConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string stringValue)
            {
                return !string.IsNullOrEmpty(stringValue);
            }
            return false;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Compares two string values and returns true if they are equal.
    /// Used for icon selection highlighting.
    /// </summary>
    public class StringEqualsConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string currentValue && parameter is string compareValue)
            {
                return currentValue == compareValue;
            }
            return false;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Returns the selected color if value equals parameter, otherwise default color.
    /// </summary>
    public class IconSelectionColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string selectedCode && parameter is string iconCode)
            {
                if (selectedCode == iconCode)
                {
                    // Selected - use primary color
                    return Application.Current?.RequestedTheme == AppTheme.Dark
                        ? Color.FromArgb("#64B5F6")
                        : Color.FromArgb("#1976D2");
                }
            }
            // Not selected - use default border color
            return Application.Current?.RequestedTheme == AppTheme.Dark
                ? Color.FromArgb("#404040")
                : Color.FromArgb("#E0E0E0");
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Returns a highlighted background color if value equals parameter, otherwise transparent.
    /// </summary>
    public class IconSelectionBackgroundConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string selectedCode && parameter is string iconCode)
            {
                if (selectedCode == iconCode)
                {
                    // Selected - use light highlight
                    return Application.Current?.RequestedTheme == AppTheme.Dark
                        ? Color.FromArgb("#1A64B5F6") // Semi-transparent blue
                        : Color.FromArgb("#E3F2FD");  // Light blue
                }
            }
            // Not selected
            return Application.Current?.RequestedTheme == AppTheme.Dark
                ? Color.FromArgb("#1E1E1E")
                : Color.FromArgb("#FFFFFF");
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts boolean IsSelected to border color.
    /// </summary>
    public class BoolToColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isSelected && isSelected)
            {
                return Application.Current?.RequestedTheme == AppTheme.Dark
                    ? Color.FromArgb("#64B5F6")
                    : Color.FromArgb("#1976D2");
            }
            return Application.Current?.RequestedTheme == AppTheme.Dark
                ? Color.FromArgb("#404040")
                : Color.FromArgb("#E0E0E0");
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts boolean IsSelected to background color.
    /// </summary>
    public class BoolToSelectedBackgroundConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isSelected && isSelected)
            {
                return Application.Current?.RequestedTheme == AppTheme.Dark
                    ? Color.FromArgb("#1A64B5F6")
                    : Color.FromArgb("#E3F2FD");
            }
            return Application.Current?.RequestedTheme == AppTheme.Dark
                ? Color.FromArgb("#1E1E1E")
                : Color.FromArgb("#FFFFFF");
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts boolean IsSelected to icon text color.
    /// </summary>
    public class BoolToIconColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isSelected && isSelected)
            {
                return Application.Current?.RequestedTheme == AppTheme.Dark
                    ? Color.FromArgb("#64B5F6")
                    : Color.FromArgb("#1976D2");
            }
            return Application.Current?.RequestedTheme == AppTheme.Dark
                ? Color.FromArgb("#FFFFFF")
                : Color.FromArgb("#333333");
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts boolean IsExpanded to FAB icon (menu/close).
    /// </summary>
    public class BoolToFabIconConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isExpanded && isExpanded)
            {
                return "\U000F0156"; // Close/X icon
            }
            return "\U000F035C"; // Menu/dots icon
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
