using System.Globalization;

namespace MoneyRecord.Converters
{
    /// <summary>
    /// Converts IsOverBudget bool to icon.
    /// </summary>
    public class BoolToOverBudgetIconConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isOverBudget && isOverBudget)
            {
                return "\U000F0029"; // alert-circle icon
            }
            return "\U000F012C"; // check-circle icon
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
