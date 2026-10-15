using MoneyRecord.Models;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Services
{
    /// <summary>Platforms without reminders yet (iOS, Mac Catalyst, Windows): Settings hides them and nothing is asked.</summary>
    public sealed class NoNotificationService : INotificationService
    {
        public bool IsSupported => false;

        public bool AreAllowed => false;

        public Task<bool> RequestPermissionAsync() => Task.FromResult(false);

        public void OpenSystemSettings()
        {
        }

        public void Show(AppNotification notification)
        {
        }
    }
}
