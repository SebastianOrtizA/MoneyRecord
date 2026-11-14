using Microsoft.Extensions.Logging;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Platforms.Windows
{
    /// <summary>
    /// What WorkManager's daily job does on Android, while the app is open (Windows has no job that runs a closed
    /// app): at start and then every hour, the reminders, recurring transactions, budget alerts and, when due, the
    /// exchange rates and the automatic backup. Each step remembers what it already did, so repeating is cheap. Runs one
    /// after another (never two at once).
    /// </summary>
    public static class WindowsDailyJob
    {
        private static readonly TimeSpan Interval = TimeSpan.FromHours(1);
        private static bool _started;

        public static void Start(IReminderService reminders, IExchangeRateService exchangeRates,
            IAutoBackupService autoBackup, ILogger logger)
        {
            if (_started)
                return;
            _started = true;
            _ = RunAsync(reminders, exchangeRates, autoBackup, logger);
        }

        private static async Task RunAsync(IReminderService reminders, IExchangeRateService exchangeRates,
            IAutoBackupService autoBackup, ILogger logger)
        {
            using var timer = new PeriodicTimer(Interval);
            var first = true;
            do
            {
                try
                {
                    await reminders.RunDailyAsync();
                    // The start already asks for the rates (App.xaml.cs): later ticks only
                    if (!first)
                        await exchangeRates.UpdateIfDueAsync();
                    // After the recurring transactions, so the backup has them (best-effort, logs its own failures)
                    await autoBackup.RunIfDueAsync();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "The periodic job failed");
                }
                first = false;
            }
            while (await timer.WaitForNextTickAsync());
        }
    }
}
