using MoneyRecord.Services.Interfaces;
using Windows.Security.Credentials.UI;

namespace MoneyRecord.Platforms.Windows
{
    /// <summary>
    /// Windows Hello (face, fingerprint, or the Hello PIN) through UserConsentVerifier. A PC without Hello reports
    /// unavailable and the app PIN is offered instead.
    /// </summary>
    public sealed class WindowsDeviceLock : IDeviceLock
    {
        public bool IsSupported => true;

        public bool IsAvailable
        {
            get
            {
                try
                {
                    return Task.Run(async () => await UserConsentVerifier.CheckAvailabilityAsync())
                        .GetAwaiter().GetResult() == UserConsentVerifierAvailability.Available;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        public async Task<bool> AuthenticateAsync(string title, string subtitle)
        {
            try
            {
                var result = await UserConsentVerifier.RequestVerificationAsync(
                    string.IsNullOrEmpty(subtitle) ? title : subtitle);
                return result == UserConsentVerificationResult.Verified;
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "Windows Hello verification failed");
                return false;
            }
        }

        public void HideContentInRecents(bool hide)
        {
            // Windows keeps no snapshot of the window in a recent-apps card
        }
    }
}
