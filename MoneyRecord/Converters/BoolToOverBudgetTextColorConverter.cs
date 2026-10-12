using System.Globalization;

namespace MoneyRecord.Converters
{
    /// <summary>
    /// Converts IsOverBudget bool to text color.
    /// </summary>
    public class BoolToOverBudgetTextColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isOverBudget && isOverBudget)
            {
                return Application.Current?.RequestedTheme == AppTheme.Dark
                    ? Color.FromArgb("#EF5350") // Light red
                    : Color.FromArgb("#D32F2F"); // Dark red
            }
            return Application.Current?.RequestedTheme == AppTheme.Dark
                ? Color.FromArgb("#66BB6A") // Light green
                : Color.FromArgb("#2E7D32"); // Dark green
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
