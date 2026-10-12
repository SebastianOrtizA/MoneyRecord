using System.Globalization;

namespace MoneyRecord.Converters
{
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
