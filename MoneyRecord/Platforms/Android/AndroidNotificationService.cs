using Android.App;
using Android.Content;
using AndroidX.Core.App;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services.Interfaces;
using Application = Android.App.Application;

namespace MoneyRecord.Platforms.Android
{
    /// <summary>
    /// Reminders as Android notifications, in two channels (bills, budgets) the user can mute separately. A tap opens the
    /// app on the notification's page (<see cref="MainActivity.RouteExtra"/>).
    /// </summary>
    public sealed class AndroidNotificationService : INotificationService
    {
        private const string BillsChannelId = "bills";
        private const string BudgetsChannelId = "budgets";

        private static Context Context => Application.Context;

        public bool IsSupported => true;

        public bool AreAllowed => NotificationManagerCompat.From(Context).AreNotificationsEnabled();

        public async Task<bool> RequestPermissionAsync()
        {
            // Android 13+ asks once (and again after a "don't allow" only from the system settings); older versions
            // allow notifications unless the user turned them off
            if (OperatingSystem.IsAndroidVersionAtLeast(33))
            {
                await MainThread.InvokeOnMainThreadAsync(Permissions.RequestAsync<Permissions.PostNotifications>);
            }
            return AreAllowed;
        }

        public void OpenSystemSettings() => AppInfo.Current.ShowSettingsUI();

        public void Show(AppNotification notification)
        {
            if (!AreAllowed)
                return;

            EnsureChannels();
            var intent = new Intent(Context, typeof(MainActivity))
                .SetFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop)
                .PutExtra(MainActivity.RouteExtra, notification.Route);
            var tap = PendingIntent.GetActivity(Context, notification.Id, intent,
                PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

            var built = new NotificationCompat.Builder(Context,
                    notification.Channel == ReminderChannel.Budgets ? BudgetsChannelId : BillsChannelId)
                .SetSmallIcon(Resource.Drawable.ic_notification)
                .SetContentTitle(notification.Title)
                .SetContentText(notification.Message)
                .SetContentIntent(tap)
                .SetAutoCancel(true)
                .Build();
            NotificationManagerCompat.From(Context).Notify(notification.Id, built);
        }

        /// <summary>Creates the channels, or renames them in the current language (Android keeps the user's settings).</summary>
        private static void EnsureChannels()
        {
            if (!OperatingSystem.IsAndroidVersionAtLeast(26))
                return;

            var manager = (NotificationManager)Context.GetSystemService(Context.NotificationService)!;
            manager.CreateNotificationChannel(new NotificationChannel(BillsChannelId, AppResources.ChannelBills,
                NotificationImportance.Default));
            manager.CreateNotificationChannel(new NotificationChannel(BudgetsChannelId, AppResources.BudgetAlerts,
                NotificationImportance.Default));
        }
    }
}
