using System.Globalization;

namespace MoneyRecord.Converters
{
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
}
