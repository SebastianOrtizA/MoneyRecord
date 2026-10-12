using MoneyRecord.Resources.Strings;
using System.Globalization;

namespace MoneyRecord.Converters
{
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
}
