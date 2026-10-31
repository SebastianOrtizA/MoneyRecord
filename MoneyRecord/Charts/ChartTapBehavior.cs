using LiveChartsCore.Drawing;
using LiveChartsCore.Kernel.Sketches;
using MoneyRecord.Helpers;
using LiveChartsCore.Measure;

namespace MoneyRecord.Charts
{
    /// <summary>
    /// Drill-down on a real tap: the chart's own pointer-down also fires when a scroll starts on the chart, so instead a
    /// tap gesture (never raised by a scroll) asks the chart which point is closest and runs that series' tap handler
    /// (<see cref="ChartFactory.HandleTap"/>). A press held longer than <see cref="PressTiming.HoldThreshold"/> only inspects (the page shows
    /// the held point's details), so releasing it is not a tap. Add it to a LiveCharts chart's Behaviors.
    /// </summary>
    public sealed class ChartTapBehavior : Behavior<View>
    {
        private readonly TapGestureRecognizer _tap = new();

        public ChartTapBehavior()
        {
            _tap.Tapped += OnTapped;
        }

        protected override void OnAttachedTo(View bindable)
        {
            base.OnAttachedTo(bindable);
            bindable.GestureRecognizers.Add(_tap);
        }

        protected override void OnDetachingFrom(View bindable)
        {
            bindable.GestureRecognizers.Remove(_tap);
            base.OnDetachingFrom(bindable);
        }

        private static void OnTapped(object? sender, TappedEventArgs e)
        {
            if (PressTiming.IsHold(TouchClock.LastDownUtc, DateTime.UtcNow))
                return;

            if (sender is not IChartView chart || sender is not View view || e.GetPosition(view) is not { } position)
                return;

            ChartFactory.HandleTap(chart.GetPointsAt(
                new LvcPointD(position.X, position.Y), FindingStrategy.CompareAllTakeClosest, FindPointFor.PointerDownEvent));
        }
    }
}
