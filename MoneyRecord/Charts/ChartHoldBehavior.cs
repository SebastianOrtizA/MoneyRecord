using LiveChartsCore;
using LiveChartsCore.Kernel;
using LiveChartsCore.Kernel.Sketches;
using MoneyRecord.Helpers;
using System.Windows.Input;

namespace MoneyRecord.Charts
{
    /// <summary>
    /// Tells <see cref="Command"/> which series is held under the finger (null once it is released), so the page can show its
    /// details in a fixed place instead of the chart's own tooltip, which opens right under the finger.
    /// The hold ends a second after the finger lifts (<see cref="TouchClock.Released"/>); the chart itself sends nothing on release.
    /// Add it to a LiveCharts chart's Behaviors next to <c>TooltipPosition="Hidden"</c>.
    /// </summary>
    public sealed class ChartHoldBehavior : Behavior<View>
    {
        public static readonly BindableProperty CommandProperty = BindableProperty.Create(
            nameof(Command), typeof(ICommand), typeof(ChartHoldBehavior));

        private IChartView? _chart;
        private readonly Debouncer _afterRelease = new(TimeSpan.FromSeconds(1));

        /// <summary>Runs with the held <see cref="ISeries"/>, or null when nothing is held.</summary>
        public ICommand? Command
        {
            get => (ICommand?)GetValue(CommandProperty);
            set => SetValue(CommandProperty, value);
        }

        protected override void OnAttachedTo(View bindable)
        {
            base.OnAttachedTo(bindable);
            BindingContext = bindable.BindingContext;
            bindable.BindingContextChanged += OnBindingContextChanged;
            TouchClock.Released += OnReleased;
            if (bindable is IChartView chart)
            {
                _chart = chart;
                chart.HoveredPointsChanged += OnHoveredPointsChanged;
            }
        }

        protected override void OnDetachingFrom(View bindable)
        {
            bindable.BindingContextChanged -= OnBindingContextChanged;
            TouchClock.Released -= OnReleased;
            _afterRelease.Cancel();
            if (_chart != null)
                _chart.HoveredPointsChanged -= OnHoveredPointsChanged;
            _chart = null;
            base.OnDetachingFrom(bindable);
        }

        private void OnBindingContextChanged(object? sender, EventArgs e) =>
            BindingContext = (sender as BindableObject)?.BindingContext;

        private void OnReleased()
        {
            var releasedAt = DateTime.UtcNow;
            _ = _afterRelease.RunAsync(() =>
            {
                // A new press began in the meantime: it owns the hold now
                if (PressTiming.MayClearAfterRelease(TouchClock.LastDownUtc, releasedAt))
                    Run(null);
            });
        }

        private void OnHoveredPointsChanged(IChartView chart, IEnumerable<ChartPoint>? newItems, IEnumerable<ChartPoint>? oldItems)
        {
            var series = newItems?.FirstOrDefault()?.Context.Series;
            Run(series);
        }

        private void Run(ISeries? series)
        {
            if (Command?.CanExecute(series) == true)
                Command.Execute(series);
        }
    }
}
