using LiveChartsCore;
using LiveChartsCore.Kernel;
using LiveChartsCore.SkiaSharpView;
using MoneyRecord.Helpers;
using MoneyRecord.Models;

namespace MoneyRecord.Charts
{
    /// <summary>
    /// Builds the Reports charts with one look: thin marks with 4px rounded ends, 2px lines with 8px points, a 2px
    /// surface gap between slices and bars, a recessive grid, money on the value axis and in the tooltips. Text stays in
    /// text colours. A tap on a slice, bar or point calls back with what was tapped (the drill-down).
    /// </summary>
    public static class ChartFactory
    {
        private const float TextSize = 11;

        // What a tap on a series does (with the tapped point's index) rides on the series itself; ChartTapBehavior runs it
        // after a real tap, so a scroll that starts on a chart never navigates
        private static void OnTap(ISeries series, Action<int> handler) => series.Tag = handler;

        /// <summary>Runs the tap handler of the first tapped point's series (see <see cref="ChartTapBehavior"/>).</summary>
        public static void HandleTap(IEnumerable<ChartPoint> points)
        {
            foreach (var point in points)
            {
                if (point.Context.Series.Tag is Action<int> handler)
                {
                    handler(point.Index);
                    return;
                }
            }
        }

        /// <summary>A named series of monthly values in one colour.</summary>
        public sealed record Line(string Name, IEnumerable<decimal> Values, Color Color);

        /// <summary>"$383.00 · 45.0%": a slice's amount and its share of the total (just the amount when there is no total).</summary>
        public static string SliceText(ChartSlice slice, decimal total, string currencyCode) => total > 0
            ? $"{MoneyFormatter.Format(slice.Amount, currencyCode)} · {slice.Amount / total:P1}"
            : MoneyFormatter.Format(slice.Amount, currencyCode);

        /// <summary>A donut: one series per slice (LiveCharts draws each pie series as a slice), no labels on it.</summary>
        public static ISeries[] Donut(IReadOnlyList<ChartSlice> slices, decimal total, string currencyCode, Action<ChartSlice> onTap) =>
            slices.Select(slice =>
            {
                var series = new PieSeries<double>
                {
                    Name = slice.Name,
                    Values = [(double)slice.Amount],
                    Fill = ChartPalette.Paint(ChartPalette.Slot(slice.PaletteSlot)),
                    Stroke = ChartPalette.Paint(ChartPalette.Surface, 2),
                    InnerRadius = 80,
                    HoverPushout = 4,
                    ToolTipLabelFormatter = _ => SliceText(slice, total, currencyCode)
                };
                OnTap(series, _ => onTap(slice));
                return (ISeries)series;
            }).ToArray();

        /// <summary>Grouped columns, one series per line; a tap calls back with the month's index and the series.</summary>
        public static ISeries[] Columns(Action<int, int> onTap, string currencyCode, params Line[] lines) =>
            lines.Select((line, seriesIndex) =>
            {
                var series = new ColumnSeries<double>
                {
                    Name = line.Name,
                    Values = line.Values.Select(v => (double)v).ToArray(),
                    Fill = ChartPalette.Paint(line.Color),
                    Rx = 4,
                    Ry = 4,
                    MaxBarWidth = 24,
                    Padding = 2,
                    YToolTipLabelFormatter = point => MoneyFormatter.Format((decimal)point.Coordinate.PrimaryValue, currencyCode)
                };
                OnTap(series, index => onTap(index, seriesIndex));
                return (ISeries)series;
            }).ToArray();

        /// <summary>Lines with points, no fill under them; a tap calls back with the month's index and the series.</summary>
        public static ISeries[] Lines(Action<int, int> onTap, string currencyCode, params Line[] lines) =>
            lines.Select((line, seriesIndex) =>
            {
                var series = new LineSeries<double>
                {
                    Name = line.Name,
                    Values = line.Values.Select(v => (double)v).ToArray(),
                    Stroke = ChartPalette.Paint(line.Color, 2),
                    Fill = null,
                    GeometrySize = 8,
                    GeometryStroke = ChartPalette.Paint(line.Color, 2),
                    GeometryFill = ChartPalette.Paint(ChartPalette.Surface),
                    LineSmoothness = 0,
                    YToolTipLabelFormatter = point => MoneyFormatter.Format((decimal)point.Coordinate.PrimaryValue, currencyCode)
                };
                OnTap(series, index => onTap(index, seriesIndex));
                return (ISeries)series;
            }).ToArray();

        /// <summary>The month axis: one label under every point or bar group, no grid lines.</summary>
        public static Axis[] LabelAxis(IEnumerable<string> labels) =>
        [
            new Axis
            {
                Labels = labels.ToArray(),
                LabelsPaint = ChartPalette.Paint(ChartPalette.AxisText),
                TextSize = TextSize,
                // A label under every bar group, not every other one
                MinStep = 1,
                ForceStepToMin = true
            }
        ];

        /// <summary>
        /// The value axis: whole money amounts with faint grid lines; from zero unless <paramref name="fromZero"/> is
        /// false (net worth and cash flow can go below zero, or sit far above it).
        /// </summary>
        public static Axis[] MoneyAxis(string currencyCode, bool fromZero = true) =>
        [
            new Axis
            {
                MinLimit = fromZero ? 0 : null,
                Labeler = value => MoneyFormatter.FormatWhole((decimal)value, currencyCode),
                LabelsPaint = ChartPalette.Paint(ChartPalette.AxisText),
                SeparatorsPaint = ChartPalette.Paint(ChartPalette.Grid, 1),
                TextSize = TextSize
            }
        ];
    }
}
