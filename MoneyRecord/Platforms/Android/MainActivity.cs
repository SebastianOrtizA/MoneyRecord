using Android.App;
using Android.Content.PM;
using Android.OS;
using AndroidX.Activity;

namespace MoneyRecord
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // Register back button callback using the modern OnBackPressedDispatcher
            OnBackPressedDispatcher.AddCallback(this, new BackPressedCallback(this));
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
                // AppShell goes to the previous page or Home (INavigationService history)
                if (Shell.Current?.SendBackButtonPressed() == true)
                    return;

                // On Home with no history, close the app
                Enabled = false;
                _activity.OnBackPressedDispatcher.OnBackPressed();
            }
        }
    }
}
