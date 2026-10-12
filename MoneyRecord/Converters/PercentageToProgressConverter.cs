using System.Globalization;

namespace MoneyRecord.Converters
{
    /// <summary>
    /// Converts percentage (0-100) to progress value (0-1).
    /// </summary>
    public class PercentageToProgressConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is double percentage)
            {
                return Math.Min(percentage / 100.0, 1.0);
            }
            return 0.0;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
