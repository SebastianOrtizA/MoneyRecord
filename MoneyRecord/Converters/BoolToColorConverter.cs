using System.Globalization;

namespace MoneyRecord.Converters
{
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
}
