using MoneyRecord.Models;
using System.Globalization;

namespace MoneyRecord.Converters
{
    public class TransactionTypeToColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is TransactionType type)
            {
                return type switch
                {
                    TransactionType.Income => TypeColors.Income,
                    // Orange for transfer; the dark one is 4.5:1 on white
                    TransactionType.Transfer => Application.Current?.RequestedTheme == AppTheme.Dark
                        ? Color.FromArgb("#FFB74D")
                        : Color.FromArgb("#B85400"),
                    _ => TypeColors.Expense
                };
            }
            return Colors.Gray;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
