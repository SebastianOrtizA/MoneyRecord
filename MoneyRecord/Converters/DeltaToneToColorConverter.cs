using MoneyRecord.Models;
using System.Globalization;

namespace MoneyRecord.Converters
{
    /// <summary>"vs previous period" colour: good news the income colour, bad news the expense colour, otherwise secondary text.</summary>
    public class DeltaToneToColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var dark = Application.Current?.RequestedTheme == AppTheme.Dark;
            var resources = Application.Current!.Resources;
            return (value as DeltaTone? ?? DeltaTone.Neutral) switch
            {
                DeltaTone.Good => resources[dark ? "IncomeColorDark" : "IncomeColor"],
                DeltaTone.Bad => resources[dark ? "ExpenseColorDark" : "ExpenseColor"],
                _ => resources[dark ? "TextSecondaryDark" : "TextSecondary"]
            };
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }
}
