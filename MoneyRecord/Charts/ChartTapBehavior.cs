using LiveChartsCore.Drawing;
using LiveChartsCore.Kernel;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.Measure;

namespace MoneyRecord.Charts
{
    /// <summary>
    /// Drill-down on a real tap: the chart's own pointer-down also fires when a scroll starts on the chart, so instead a
    /// tap gesture (never raised by a scroll) asks the chart which point is closest and runs that series' tap handler
    /// (<see cref="ChartFactory.HandleTap"/>). A press held longer than <see cref="HoldThreshold"/> only inspects (the page shows the
    /// held point's details), so releasing it is not a tap. Add it to a LiveCharts chart's Behaviors.
    /// </summary>
    public sealed class ChartTapBehavior : Behavior<View>
    {
        private readonly TapGestureRecognizer _tap = new();

        /// <summary>Android's long-press time: a press held this long is not a tap.</summary>
        private static readonly TimeSpan HoldThreshold = TimeSpan.FromMilliseconds(400);

        private IChartView? _chart;
        private DateTime? _pressStart;
        private bool _hovering;

        public ChartTapBehavior()
        {
            _tap.Tapped += OnTapped;
        }

        protected override void OnAttachedTo(View bindable)
        {
            base.OnAttachedTo(bindable);
            bindable.GestureRecognizers.Add(_tap);
            if (bindable is IChartView chart)
            {
                _chart = chart;
                chart.HoveredPointsChanged += OnHoveredPointsChanged;
            }
        }

        protected override void OnDetachingFrom(View bindable)
        {
            bindable.GestureRecognizers.Remove(_tap);
            if (_chart != null)
                _chart.HoveredPointsChanged -= OnHoveredPointsChanged;
            _chart = null;
            base.OnDetachingFrom(bindable);
        }

        // The chart reports the point under the finger when a press starts (that is what opens its tooltip): the time of the
        // first report of a press is when the press began
        private void OnHoveredPointsChanged(IChartView chart, IEnumerable<ChartPoint>? newItems, IEnumerable<ChartPoint>? oldItems)
        {
            if (newItems?.Any() != true)
            {
                _hovering = false;
                return;
            }

            if (!_hovering)
                _pressStart = DateTime.UtcNow;
            _hovering = true;
        }

        private void OnTapped(object? sender, TappedEventArgs e)
        {
            var heldFor = _pressStart is { } start ? DateTime.UtcNow - start : TimeSpan.Zero;
            _pressStart = null;
            if (heldFor > HoldThreshold)
                return;

            if (sender is not IChartView chart || sender is not View view || e.GetPosition(view) is not { } position)
                return;

            ChartFactory.HandleTap(chart.GetPointsAt(
                new LvcPointD(position.X, position.Y), FindingStrategy.CompareAllTakeClosest, FindPointFor.PointerDownEvent));
        }
    }
}
