using MoneyRecord.Helpers;
using System.Globalization;

namespace MoneyRecord.Converters
{
    /// <summary>Formats a DateTime in the current culture's word order; ConverterParameter is a <see cref="DateStyle"/>.</summary>
    public class DateTextConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not DateTime date)
                return string.Empty;

            var style = Enum.TryParse<DateStyle>(parameter as string, out var parsed) ? parsed : DateStyle.Medium;
            return DateFormatter.Format(date, style);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
