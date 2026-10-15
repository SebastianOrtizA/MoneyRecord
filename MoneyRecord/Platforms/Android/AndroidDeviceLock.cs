using AndroidX.Biometric;
using AndroidX.Core.Content;
using AndroidX.Fragment.App;
using MoneyRecord.Services.Interfaces;
using Application = Android.App.Application;

namespace MoneyRecord.Platforms.Android
{
    /// <summary>
    /// The app lock on Android: BiometricPrompt with fingerprint / face (weak biometrics are enough to open the app) or the
    /// phone's PIN, pattern or password. The app stores no secret of its own.
    /// </summary>
    public sealed class AndroidDeviceLock : IDeviceLock
    {
        private const int Authenticators =
            BiometricManager.Authenticators.BiometricWeak | BiometricManager.Authenticators.DeviceCredential;

        public bool IsSupported => true;

        public bool IsAvailable =>
            BiometricManager.From(Application.Context).CanAuthenticate(Authenticators) == BiometricManager.BiometricSuccess;

        public Task<bool> AuthenticateAsync(string title, string subtitle)
        {
            if (Platform.CurrentActivity is not FragmentActivity activity || !IsAvailable)
                return Task.FromResult(false);

            var result = new TaskCompletionSource<bool>();
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var prompt = new BiometricPrompt(activity, ContextCompat.GetMainExecutor(activity), new Callback(result));
                var info = new BiometricPrompt.PromptInfo.Builder()
                    .SetTitle(title)
                    .SetSubtitle(subtitle)
                    .SetAllowedAuthenticators(Authenticators)
                    .Build();
                prompt.Authenticate(info);
            });
            return result.Task;
        }

        public void HideContentInRecents(bool hide)
        {
            // Android 13+: the recent apps screen shows a blank card instead of the balances (screenshots still work)
            if (OperatingSystem.IsAndroidVersionAtLeast(33) && Platform.CurrentActivity is { } activity)
                activity.SetRecentsScreenshotEnabled(!hide);
        }

        private sealed class Callback(TaskCompletionSource<bool> result) : BiometricPrompt.AuthenticationCallback
        {
            public override void OnAuthenticationSucceeded(BiometricPrompt.AuthenticationResult result1) =>
                result.TrySetResult(true);

            // Cancelled, too many attempts, no lock set... (a single wrong finger only shows a retry in the prompt)
            public override void OnAuthenticationError(int errorCode, Java.Lang.ICharSequence errString) =>
                result.TrySetResult(false);
        }
    }
}
