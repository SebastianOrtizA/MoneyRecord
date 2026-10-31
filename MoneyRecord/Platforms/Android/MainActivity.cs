using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Activity;

namespace MoneyRecord
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        /// <summary>The page a tapped notification opens (a Shell route).</summary>
        public const string RouteExtra = "route";

        // Activities started for a result (the backup folder picker), by request code. Static: Android may recreate the
        // activity while the picker is open, and the new instance gets the result
        private static readonly Dictionary<int, TaskCompletionSource<Intent?>> PendingResults = [];
        private static int _lastRequestCode = 1000;

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // Register back button callback using the modern OnBackPressedDispatcher
            OnBackPressedDispatcher.AddCallback(this, new BackPressedCallback(this));
        }

        /// <summary>Starts an activity and returns its result data; null when it was cancelled.</summary>
        public Task<Intent?> StartForResultAsync(Intent intent)
        {
            var requestCode = ++_lastRequestCode;
            var result = new TaskCompletionSource<Intent?>();
            PendingResults[requestCode] = result;
            StartActivityForResult(intent, requestCode);
            return result.Task;
        }

        protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
        {
            base.OnActivityResult(requestCode, resultCode, data);
            if (PendingResults.Remove(requestCode, out var result))
                result.TrySetResult(resultCode == Result.Ok ? data : null);
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
