using Android.Content;
using Android.Runtime;
using AndroidX.Work;
using Microsoft.Extensions.Logging;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Platforms.Android
{
    /// <summary>
    /// The daily background job (WorkManager): reminders, recurring transactions, budget alerts, exchange rates and
    /// automatic backups while the app is closed. The Java name is fixed because WorkManager stores it with the
    /// scheduled work.
    /// </summary>
    [Register("com.sebastianortiz.moneyrecord.DailyReminderWorker")]
    public sealed class DailyReminderWorker(Context context, WorkerParameters parameters) : Worker(context, parameters)
    {
        private const string WorkName = "daily-reminders";

        /// <summary>The hour of the day the job aims for (WorkManager may run it later, e.g. in battery saving).</summary>
        private const int RunHour = 9;

        /// <summary>Schedules the job once a day; an existing schedule is kept, so calling it at every start is cheap.</summary>
        public static void Schedule(Context context)
        {
            var now = DateTime.Now;
            var firstRun = now.Date.AddHours(RunHour);
            if (firstRun <= now)
                firstRun = firstRun.AddDays(1);

            var request = new PeriodicWorkRequest.Builder(typeof(DailyReminderWorker), TimeSpan.FromDays(1))
                .SetInitialDelay((long)(firstRun - now).TotalMinutes, Java.Util.Concurrent.TimeUnit.Minutes!)
                .Build();
            WorkManager.GetInstance(context)
                .EnqueueUniquePeriodicWork(WorkName, ExistingPeriodicWorkPolicy.Keep!, (PeriodicWorkRequest)request);
        }

        public override Result DoWork()
        {
            // The app's services: Android created the MAUI application (MauiProgram) for this process
            var services = IPlatformApplication.Current?.Services;
            if (services == null)
                return Result.InvokeRetry();

            try
            {
                services.GetRequiredService<IReminderService>().RunDailyAsync().GetAwaiter().GetResult();
                // Opt-in and best-effort (logs its own failures): a missing connection never fails the reminders
                services.GetRequiredService<IExchangeRateService>().UpdateIfDueAsync().GetAwaiter().GetResult();
                // Also best-effort: after the recurring transactions, so the backup has them
                services.GetRequiredService<IAutoBackupService>().RunIfDueAsync().GetAwaiter().GetResult();
                // The recurring transactions and debt payments it created change the widgets' balances
                services.GetRequiredService<IHomeScreenWidgets>().Refresh();
                return Result.InvokeSuccess();
            }
            catch (Exception ex)
            {
                services.GetRequiredService<ILogger<DailyReminderWorker>>().LogError(ex, "Daily reminders failed");
                return Result.InvokeFailure();
            }
        }
    }
}
