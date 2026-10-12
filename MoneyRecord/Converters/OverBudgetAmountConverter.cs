using MoneyRecord.Helpers;
using System.Globalization;

namespace MoneyRecord.Converters
{
    /// <summary>
    /// Multi-value converter that returns exceeded amount or remaining amount based on IsOverBudget.
    /// </summary>
    public class OverBudgetAmountConverter : IMultiValueConverter
    {
        public object? Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Length >= 3 &&
                values[0] is bool isOverBudget &&
                values[1] is decimal exceededAmount &&
                values[2] is decimal remainingAmount)
            {
                var amount = isOverBudget ? exceededAmount : remainingAmount;
                return MoneyFormatter.Format(amount);
            }
            return MoneyFormatter.Format(0);
        }

        public object?[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
