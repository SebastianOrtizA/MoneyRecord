using Android.Views;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Platforms.Android
{
    /// <summary>
    /// FLAG_SECURE on the activity window: no screenshots, no screen recording, a blank card in the recent apps. Works on
    /// every Android version.
    /// </summary>
    public sealed class AndroidScreenProtection : IScreenProtection
    {
        public bool IsSupported => true;

        public void SetSecure(bool secure)
        {
            void Apply()
            {
                if (Platform.CurrentActivity?.Window is not { } window)
                    return;
                if (secure)
                    window.AddFlags(WindowManagerFlags.Secure);
                else
                    window.ClearFlags(WindowManagerFlags.Secure);
            }

            if (MainThread.IsMainThread)
                Apply();
            else
                MainThread.BeginInvokeOnMainThread(Apply);
        }
    }
}
