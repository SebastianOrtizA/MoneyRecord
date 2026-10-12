using System.Globalization;

namespace MoneyRecord.Converters
{
    /// <summary>
    /// Converts IsOverBudget bool to background color.
    /// </summary>
    public class BoolToOverBudgetColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isOverBudget && isOverBudget)
            {
                return Application.Current?.RequestedTheme == AppTheme.Dark
                    ? Color.FromArgb("#4D1F1F") // Dark red background
                    : Color.FromArgb("#FFEBEE"); // Light red background
            }
            return Application.Current?.RequestedTheme == AppTheme.Dark
                ? Color.FromArgb("#1F3D1F") // Dark green background
                : Color.FromArgb("#E8F5E9"); // Light green background
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
