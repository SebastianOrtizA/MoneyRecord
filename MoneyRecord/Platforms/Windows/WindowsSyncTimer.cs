using Microsoft.Extensions.Logging;
using MoneyRecord.Services.Sync;

namespace MoneyRecord.Platforms.Windows
{
    /// <summary>
    /// While the computer's app is open, writes what changed here into the store the phone reads from (the phone connects; the
    /// computer only waits), and applies what the phone left. A no-op while sync is off.
    /// </summary>
    public static class WindowsSyncTimer
    {
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(2);
        private static bool _started;

        public static void Start(ISyncService sync, ILogger logger)
        {
            if (_started)
                return;
            _started = true;
            _ = RunAsync(sync, logger);
        }

        private static async Task RunAsync(ISyncService sync, ILogger logger)
        {
            using var timer = new PeriodicTimer(Interval);
            while (await timer.WaitForNextTickAsync())
            {
                try
                {
                    await sync.SyncNowAsync();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "The periodic sync failed");
                }
            }
        }
    }
}
