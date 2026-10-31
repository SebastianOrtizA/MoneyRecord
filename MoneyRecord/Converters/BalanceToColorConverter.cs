using System.Globalization;

namespace MoneyRecord.Converters
{
    public class BalanceToColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is decimal balance ? TypeColors.ForSign(balance) : Colors.Gray;

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
