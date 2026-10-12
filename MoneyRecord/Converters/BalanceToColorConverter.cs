using System.Globalization;

namespace MoneyRecord.Converters
{
    public class BalanceToColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is decimal balance)
            {
                if (balance >= 0)
                {
                    // Green for positive balance
                    return Application.Current?.RequestedTheme == AppTheme.Dark
                        ? Color.FromArgb("#66BB6A")
                        : Color.FromArgb("#2E7D32");
                }
                else
                {
                    // Red for negative balance
                    return Application.Current?.RequestedTheme == AppTheme.Dark
                        ? Color.FromArgb("#EF5350")
                        : Color.FromArgb("#D32F2F");
                }
            }
            return Colors.Gray;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
