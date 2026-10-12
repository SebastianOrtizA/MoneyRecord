using MoneyRecord.Models;
using System.Globalization;

namespace MoneyRecord.Converters
{
    /// <summary>
    /// Budget progress colour. Amber instead of yellow, which has no contrast on a light background
    /// (#B26A00 light / #F2B65C dark). Only for the bar: amounts keep the normal text colour.
    /// </summary>
    public class BudgetLevelToColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var dark = Application.Current?.RequestedTheme == AppTheme.Dark;
            return (value as BudgetLevel? ?? BudgetLevel.OnTrack) switch
            {
                BudgetLevel.Exceeded => Color.FromArgb(dark ? "#EF5350" : "#D32F2F"),
                BudgetLevel.High => Color.FromArgb(dark ? "#FF8A65" : "#E65100"),
                BudgetLevel.Warning => Color.FromArgb(dark ? "#F2B65C" : "#B26A00"),
                _ => Color.FromArgb(dark ? "#66BB6A" : "#2E7D32")
            };
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
