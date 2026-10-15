using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Activity;
using MoneyRecord.Services;

namespace MoneyRecord
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        /// <summary>The page a tapped notification opens (a Shell route).</summary>
        public const string RouteExtra = "route";

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // Register back button callback using the modern OnBackPressedDispatcher
            OnBackPressedDispatcher.AddCallback(this, new BackPressedCallback(this));

            // Opened from a notification: Home opens its page once the Shell is ready (not again after a rotation)
            if (savedInstanceState == null)
                TakeNotificationRoute(Intent, openNow: false);
        }

        protected override void OnNewIntent(Intent? intent)
        {
            base.OnNewIntent(intent);
            // The app was running: open the page now
            TakeNotificationRoute(intent, openNow: true);
        }

        private static void TakeNotificationRoute(Intent? intent, bool openNow)
        {
            var route = intent?.GetStringExtra(RouteExtra);
            var launch = IPlatformApplication.Current?.Services.GetService<NotificationLaunch>();
            if (string.IsNullOrEmpty(route) || launch == null)
                return;

            launch.Request(route);
            if (openNow && Shell.Current != null)
                MainThread.BeginInvokeOnMainThread(async () => await launch.OpenPendingAsync());
        }

        private class BackPressedCallback : OnBackPressedCallback
        {
            private readonly MainActivity _activity;

            public BackPressedCallback(MainActivity activity) : base(true)
            {
                _activity = activity;
            }

            public override void HandleOnBackPressed()
            {
                // AppShell goes to the previous page or Home (INavigationService history); the first-run setup, shown
                // before the Shell exists, goes to its previous step
                var page = Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault()?.Page;
                if (page?.SendBackButtonPressed() == true)
                    return;

                // On Home with no history, let the system close the app. Re-enable afterwards: on Android 12+
                // the activity survives in the background, and back must reach AppShell again after it resumes
                Enabled = false;
                _activity.OnBackPressedDispatcher.OnBackPressed();
                Enabled = true;
            }
        }
    }
}
