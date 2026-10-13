using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace MoneyRecord.Charts
{
    /// <summary>
    /// Chart colours, light and dark each chosen for their own surface (not a flip): the dataviz reference
    /// categorical palette in its fixed order, a neutral grey for "Other", recessive ink for axes and grid, and the
    /// app's income / expense colours (read from <c>Colors.xaml</c>). Charts are built when a page loads, so a theme
    /// switch shows on the next load.
    /// </summary>
    public static class ChartPalette
    {
        private static readonly string[] CategoricalLight =
            ["#2a78d6", "#eb6834", "#1baf7a", "#eda100", "#e87ba4", "#008300", "#6250d6", "#e34948"];

        private static readonly string[] CategoricalDark =
            ["#3987e5", "#d95926", "#199e70", "#c98500", "#d55181", "#008300", "#9085e9", "#e66767"];

        public static bool IsDark => Application.Current?.RequestedTheme == AppTheme.Dark;

        /// <summary>A category's colour by its palette slot (<see cref="Models.CategoryAmount.PaletteSlot"/>); grey for "Other".</summary>
        public static Color Slot(int slot)
        {
            var palette = IsDark ? CategoricalDark : CategoricalLight;
            return slot >= 0 && slot < palette.Length
                ? Color.FromArgb(palette[slot])
                : Color.FromArgb(IsDark ? "#7a7974" : "#a3a29c");
        }

        public static Color Income => Resource(IsDark ? "IncomeColorDark" : "IncomeColor");

        public static Color Expense => Resource(IsDark ? "ExpenseColorDark" : "ExpenseColor");

        /// <summary>The card surface behind the charts (<c>ChartCard</c>): the 2px gap between slices and bars is drawn in it.</summary>
        public static Color Surface => Resource(IsDark ? "CardBackgroundDark" : "CardBackground");

        /// <summary>Axis labels: secondary ink, never a series colour.</summary>
        public static Color AxisText => Color.FromArgb(IsDark ? "#AAAAAA" : "#666666");

        /// <summary>Grid lines: recessive.</summary>
        public static Color Grid => Color.FromArgb(IsDark ? "#333333" : "#EEEEEE");

        /// <summary>An app colour from <c>Colors.xaml</c>, so the charts follow the app's income / expense / card colours.</summary>
        private static Color Resource(string key) => (Color)Application.Current!.Resources[key];

        public static SolidColorPaint Paint(Color color, float strokeThickness = 0) =>
            new(ToSk(color)) { StrokeThickness = strokeThickness };

        public static SKColor ToSk(Color color)
        {
            color.ToRgba(out var r, out var g, out var b, out var a);
            return new SKColor(r, g, b, a);
        }
    }
}
