using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using System.Globalization;

namespace MoneyRecord.Converters
{
    public class BoolToGroupTextConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is Models.GroupingMode mode)
            {
                return mode switch
                {
                    Models.GroupingMode.None => AppResources.ListView,
                    Models.GroupingMode.Category => AppResources.GroupByCategory,
                    Models.GroupingMode.Account => AppResources.GroupByAccount,
                    _ => AppResources.ListView
                };
            }
            // Backward compatibility with bool
            if (value is bool isGrouped)
            {
                return isGrouped ? AppResources.Grouped : AppResources.ListView;
            }
            return AppResources.ListView;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
