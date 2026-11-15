using Android.Content;
using Android.Runtime;
using AndroidX.Work;
using Microsoft.Extensions.Logging;
using MoneyRecord.Services.Sync;

namespace MoneyRecord.Platforms.Android
{
    /// <summary>
    /// The phone syncs with the computer in the background (WorkManager): every hour while on an unmetered network. When the
    /// computer is off or on another network it simply finds nobody and tries again next time. The Java name is fixed because
    /// WorkManager stores it with the scheduled work.
    /// </summary>
    [Register("com.sebastianortiz.moneyrecord.SyncWorker")]
    public sealed class SyncWorker(Context context, WorkerParameters parameters) : Worker(context, parameters)
    {
        private const string WorkName = "sync";

        public static void Schedule(Context context)
        {
            var constraints = new Constraints.Builder().SetRequiredNetworkType(NetworkType.Unmetered!).Build();
            var request = new PeriodicWorkRequest.Builder(typeof(SyncWorker), TimeSpan.FromHours(1))
                .SetConstraints(constraints)
                .Build();
            WorkManager.GetInstance(context)
                .EnqueueUniquePeriodicWork(WorkName, ExistingPeriodicWorkPolicy.Keep!, (PeriodicWorkRequest)request);
        }

        public static void Cancel(Context context) => WorkManager.GetInstance(context).CancelUniqueWork(WorkName);

        public override Result DoWork()
        {
            var services = IPlatformApplication.Current?.Services;
            if (services == null)
                return Result.InvokeRetry();

            try
            {
                // Nobody answering is normal (the computer is off): not a failure, the next run tries again
                services.GetRequiredService<ISyncService>().SyncNowAsync().GetAwaiter().GetResult();
                // What came from the computer may change the widgets' balances
                services.GetRequiredService<MoneyRecord.Services.Interfaces.IHomeScreenWidgets>().RefreshAsync().GetAwaiter().GetResult();
                return Result.InvokeSuccess();
            }
            catch (Exception ex)
            {
                services.GetRequiredService<ILogger<SyncWorker>>().LogError(ex, "Background sync failed");
                return Result.InvokeFailure();
            }
        }
    }

    public sealed class AndroidSyncBackground : ISyncBackground
    {
        public void Schedule() => SyncWorker.Schedule(global::Android.App.Application.Context);

        public void Cancel() => SyncWorker.Cancel(global::Android.App.Application.Context);
    }
}
