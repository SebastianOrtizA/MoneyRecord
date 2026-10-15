using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Services
{
    /// <summary>Platforms without the app lock yet (iOS, Mac Catalyst, Windows): Settings hides it.</summary>
    public sealed class NoDeviceLock : IDeviceLock
    {
        public bool IsSupported => false;

        public bool IsAvailable => false;

        public Task<bool> AuthenticateAsync(string title, string subtitle) => Task.FromResult(false);

        public void HideContentInRecents(bool hide)
        {
        }
    }
}
