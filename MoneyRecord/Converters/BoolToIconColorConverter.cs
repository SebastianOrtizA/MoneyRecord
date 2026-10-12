using System.Globalization;

namespace MoneyRecord.Converters
{
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
}
