using System.Globalization;

namespace MoneyRecord.Converters
{
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
}
