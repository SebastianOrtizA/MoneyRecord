using MoneyRecord.Models;
using System.Globalization;

namespace MoneyRecord.Converters
{
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
}
