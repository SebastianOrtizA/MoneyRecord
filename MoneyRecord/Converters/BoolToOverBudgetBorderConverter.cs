using System.Globalization;

namespace MoneyRecord.Converters
{
    /// <summary>
    /// Converts IsOverBudget bool to border color.
    /// </summary>
    public class BoolToOverBudgetBorderConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isOverBudget && isOverBudget)
            {
                return Application.Current?.RequestedTheme == AppTheme.Dark
                    ? Color.FromArgb("#EF5350") // Light red border
                    : Color.FromArgb("#D32F2F"); // Dark red border
            }
            return Application.Current?.RequestedTheme == AppTheme.Dark
                ? Color.FromArgb("#66BB6A") // Light green border
                : Color.FromArgb("#2E7D32"); // Dark green border
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
