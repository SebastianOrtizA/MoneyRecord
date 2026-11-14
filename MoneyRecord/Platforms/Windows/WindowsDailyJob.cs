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
                // Each step on its own: a failing one never keeps the backup from running
                await Step(reminders.RunDailyAsync, "daily", logger);
                // The start already asks for the rates (App.xaml.cs): later ticks only
                if (!first)
                    await Step(exchangeRates.UpdateIfDueAsync, "rates", logger);
                // After the recurring transactions, so the backup has them (best-effort, logs its own failures)
                await Step(autoBackup.RunIfDueAsync, "backup", logger);
                first = false;
            }
            while (await timer.WaitForNextTickAsync());
        }

        private static async Task Step(Func<Task> step, string name, ILogger logger)
        {
            try
            {
                await step();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "The periodic job step {Step} failed", name);
            }
        }
    }
}
