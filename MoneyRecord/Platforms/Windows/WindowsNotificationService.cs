using Microsoft.Extensions.Logging;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using MoneyRecord.Models;
using AppNotification = MoneyRecord.Models.AppNotification;
using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Platforms.Windows
{
    /// <summary>
    /// Reminders as Windows toast notifications (Windows App SDK; works for the unpackaged zip build). The channels
    /// (bills, budgets) don't exist on Windows: the app has one switch in the system's notification settings. A tap on
    /// a toast while the app runs opens its page; a toast that starts the app only brings it up.
    /// </summary>
    public sealed class WindowsNotificationService : INotificationService
    {
        private const string RouteKey = "route";
        private readonly Lazy<bool> _registered;

        public WindowsNotificationService(NotificationLaunch launch, ILogger<WindowsNotificationService> logger)
        {
            // On first use, not while the app is being built
            _registered = new Lazy<bool>(() => Register(launch, logger));
        }

        private static bool Register(NotificationLaunch launch, ILogger logger)
        {
            try
            {
                AppNotificationManager.Default.NotificationInvoked += (_, args) =>
                {
                    if (!args.Arguments.TryGetValue(RouteKey, out var route) || string.IsNullOrEmpty(route))
                        return;
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        launch.Request(route);
                        await launch.OpenPendingAsync();
                    });
                };
                try
                {
                    AppNotificationManager.Default.Register();
                }
                catch (ArgumentException)
                {
                    // "Already registered": the process is registered already (by the Windows App SDK itself); fine
                }
                return true;
            }
            catch (Exception ex)
            {
                // No toasts (a locked-down PC): reminders stay hidden in Settings
                logger.LogWarning(ex, "Windows notifications can't be registered");
                return false;
            }
        }

        public bool IsSupported => _registered.Value;

        public bool AreAllowed => _registered.Value && AppNotificationManager.Default.Setting == AppNotificationSetting.Enabled;

        // Windows asks nothing: the user turns the app's notifications off in the system settings
        public Task<bool> RequestPermissionAsync() => Task.FromResult(AreAllowed);

        public void OpenSystemSettings() => _ = Launcher.Default.OpenAsync("ms-settings:notifications");

        public void Show(AppNotification notification)
        {
            if (!AreAllowed)
                return;

            var toast = new AppNotificationBuilder()
                .AddText(notification.Title)
                .AddText(notification.Message)
                .AddArgument(RouteKey, notification.Route)
                .BuildNotification();
            // The same tag replaces the previous toast, like the notification ID on Android
            toast.Tag = notification.Id.ToString();
            toast.Group = notification.Channel.ToString();
            AppNotificationManager.Default.Show(toast);
        }
    }
}
