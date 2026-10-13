using MoneyRecord.Charts;
using System.Globalization;

namespace MoneyRecord.Converters
{
    /// <summary>A category's chart colour from its palette slot, so a list row's dot matches its donut slice.</summary>
    public class PaletteSlotToColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            ChartPalette.Slot(value is int slot ? slot : -1);

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }
}
