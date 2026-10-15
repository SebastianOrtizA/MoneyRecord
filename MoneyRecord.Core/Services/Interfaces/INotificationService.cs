using MoneyRecord.Models;

namespace MoneyRecord.Services.Interfaces
{
    /// <summary>System notifications (Android). Other platforms don't support them yet: nothing is shown or asked.</summary>
    public interface INotificationService
    {
        bool IsSupported { get; }

        /// <summary>The system lets the app post notifications (Android 13+ asks; the user can also turn them off).</summary>
        bool AreAllowed { get; }

        /// <summary>Asks for the permission when the system needs it; true when notifications are allowed.</summary>
        Task<bool> RequestPermissionAsync();

        /// <summary>The app's page in the system settings, where blocked notifications are allowed again.</summary>
        void OpenSystemSettings();

        void Show(AppNotification notification);
    }
}
