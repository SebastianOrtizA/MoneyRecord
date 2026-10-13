using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using MoneyRecord.Helpers;
using MoneyRecord.Models;

namespace MoneyRecord.Charts
{
    /// <summary>
    /// Builds the Reports charts with one look: thin marks with 4px rounded ends, a 2px surface gap between slices and
    /// bars, a recessive grid, money on the value axis and in the tooltips. Text stays in text colours.
    /// </summary>
    public static class ChartFactory
    {
        private const float TextSize = 11;

        /// <summary>A donut: one series per slice (LiveCharts draws each pie series as a slice), no labels on it.</summary>
        public static ISeries[] Donut(IReadOnlyList<ChartSlice> slices, decimal total) =>
            slices.Select(slice => (ISeries)new PieSeries<double>
            {
                Name = slice.Name,
                Values = [(double)slice.Amount],
                Fill = ChartPalette.Paint(ChartPalette.Slot(slice.PaletteSlot)),
                Stroke = ChartPalette.Paint(ChartPalette.Surface, 2),
                InnerRadius = 80,
                HoverPushout = 4,
                // Shows on hover (Windows); a tap on Android shows none, so the ranked list carries the values
                ToolTipLabelFormatter = _ => total > 0
                    ? $"{MoneyFormatter.Format(slice.Amount)} · {slice.Amount / total:P1}"
                    : MoneyFormatter.Format(slice.Amount)
            }).ToArray();

        /// <summary>Grouped columns, one series per (name, values, colour); the tooltip shows the money amount.</summary>
        public static ISeries[] Columns(params (string Name, IEnumerable<decimal> Values, Color Color)[] series) =>
            series.Select(s => (ISeries)new ColumnSeries<double>
            {
                Name = s.Name,
                Values = s.Values.Select(v => (double)v).ToArray(),
                Fill = ChartPalette.Paint(s.Color),
                Rx = 4,
                Ry = 4,
                MaxBarWidth = 24,
                Padding = 2,
                YToolTipLabelFormatter = point => MoneyFormatter.Format((decimal)point.Coordinate.PrimaryValue)
            }).ToArray();

        /// <summary>The month axis: one label per bar group, no grid lines.</summary>
        public static Axis[] LabelAxis(IEnumerable<string> labels) =>
        [
            new Axis
            {
                Labels = labels.ToArray(),
                // A label under every bar group, not every other one
                MinStep = 1,
                ForceStepToMin = true,
                LabelsPaint = ChartPalette.Paint(ChartPalette.AxisText),
                TextSize = TextSize
            }
        ];

        /// <summary>The value axis: whole money amounts from zero, with faint grid lines.</summary>
        public static Axis[] MoneyAxis() =>
        [
            new Axis
            {
                MinLimit = 0,
                Labeler = value => MoneyFormatter.FormatWhole((decimal)value),
                LabelsPaint = ChartPalette.Paint(ChartPalette.AxisText),
                SeparatorsPaint = ChartPalette.Paint(ChartPalette.Grid, 1),
                TextSize = TextSize
            }
        ];
    }
}
