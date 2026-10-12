using MoneyRecord.Models;
using System.Globalization;

namespace MoneyRecord.Converters
{
    /// <summary>
    /// Heatmap colours of the category reports: red shades for expenses, green to teal for incomes.
    /// The value is a <see cref="CategoryAmount"/> (its level), or a <see cref="CategoryType"/> with the parameter
    /// Low, Medium or High for the legend. <see cref="Text"/> = true gives a readable text colour on that background.
    /// </summary>
    public class HeatmapColorConverter : IValueConverter
    {
        private static readonly Color[] ExpenseColors =
        [
            Color.FromArgb("#FFFFFF"), Color.FromArgb("#FFF5F5"), Color.FromArgb("#FFEBEE"), Color.FromArgb("#FFCDD2"),
            Color.FromArgb("#EF9A9A"), Color.FromArgb("#E57373"), Color.FromArgb("#EF5350"), Color.FromArgb("#F44336"),
            Color.FromArgb("#E53935"), Color.FromArgb("#D32F2F"), Color.FromArgb("#C62828"), Color.FromArgb("#B71C1C")
        ];

        private static readonly Color[] IncomeColors =
        [
            Color.FromArgb("#E8F5E9"), Color.FromArgb("#C8E6C9"), Color.FromArgb("#A5D6A7"), Color.FromArgb("#81C784"),
            Color.FromArgb("#66BB6A"), Color.FromArgb("#4CAF50"), Color.FromArgb("#43A047"), Color.FromArgb("#388E3C"),
            Color.FromArgb("#2E7D32"), Color.FromArgb("#1B5E20"), Color.FromArgb("#00796B"), Color.FromArgb("#004D40")
        ];

        /// <summary>False: the background colour; true: black or white text for it.</summary>
        public bool Text { get; set; }

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var (type, level) = value switch
            {
                CategoryAmount amount => (amount.Type, amount.HeatLevel),
                CategoryType legendType => (legendType, LegendLevel(legendType, parameter as string)),
                _ => (CategoryType.Expense, 0)
            };

            var palette = type == CategoryType.Income ? IncomeColors : ExpenseColors;
            var background = palette[Math.Clamp(level, 0, palette.Length - 1)];
            return Text ? ContrastText(background) : background;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// The legend's "Medium" swatch, as the two report pages had it: the expense palette starts with four near-white
        /// pinks, so its first clearly pink shade (#FFCDD2, level 3) reads as medium; the income palette is evenly
        /// spread, so its middle green (#4CAF50, level 5) does.
        /// </summary>
        private static int LegendLevel(CategoryType type, string? swatch) => swatch switch
        {
            "Low" => 0,
            "High" => CategoryAmount.HeatLevels - 1,
            _ => type == CategoryType.Income ? 5 : 3
        };

        private static Color ContrastText(Color background)
        {
            var luminance = 0.299 * background.Red + 0.587 * background.Green + 0.114 * background.Blue;
            return luminance > 0.5 ? Colors.Black : Colors.White;
        }
    }
}
