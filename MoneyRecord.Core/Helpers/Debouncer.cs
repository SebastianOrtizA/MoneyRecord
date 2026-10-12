namespace MoneyRecord.Helpers
{
    /// <summary>
    /// Runs an action once input has been quiet for <c>delay</c>: each <see cref="RunAsync"/> call cancels the
    /// pending one, so typing "coffee" filters once instead of six times.
    /// </summary>
    public sealed class Debouncer
    {
        private readonly TimeSpan _delay;
        private readonly TimeProvider _timeProvider;
        private CancellationTokenSource? _pending;

        /// <param name="timeProvider">The clock; tests pass a fake one. Default: the system clock.</param>
        public Debouncer(TimeSpan delay, TimeProvider? timeProvider = null)
        {
            _delay = delay;
            _timeProvider = timeProvider ?? TimeProvider.System;
        }

        /// <summary>Waits for the delay, then runs <paramref name="action"/> unless another call or <see cref="Cancel"/> came first.</summary>
        public async Task RunAsync(Action action)
        {
            Cancel();
            var pending = _pending = new CancellationTokenSource();

            try
            {
                await Task.Delay(_delay, _timeProvider, pending.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            // The delay may have ended just before a newer call or Cancel(): only the latest call runs
            if (pending != _pending)
                return;

            _pending = null;
            pending.Dispose();
            action();
        }

        /// <summary>Drops the pending action, e.g. when the filter is applied right away.</summary>
        public void Cancel()
        {
            var pending = _pending;
            _pending = null;
            pending?.Cancel();
            pending?.Dispose();
        }
    }
}
