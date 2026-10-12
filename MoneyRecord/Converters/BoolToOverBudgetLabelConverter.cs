using MoneyRecord.Resources.Strings;
using System.Globalization;

namespace MoneyRecord.Converters
{
    /// <summary>
    /// Converts IsOverBudget bool to label text.
    /// </summary>
    public class BoolToOverBudgetLabelConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isOverBudget && isOverBudget)
            {
                return AppResources.Exceeded;
            }
            return AppResources.Remaining;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
