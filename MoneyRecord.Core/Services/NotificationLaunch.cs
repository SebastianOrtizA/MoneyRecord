using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Services
{
    /// <summary>
    /// A tapped notification's page (singleton). Android hands it over when the app opens; it is opened at once when the
    /// app is already running, otherwise when Home first appears (the Shell isn't ready before that).
    /// </summary>
    public sealed class NotificationLaunch(INavigationService navigation)
    {
        private string? _pending;

        public void Request(string route) => _pending = route;

        /// <summary>Opens the pending page, once; does nothing when there is none.</summary>
        public async Task OpenPendingAsync()
        {
            var route = _pending;
            _pending = null;
            if (route != null)
                await navigation.NavigateToAsync(route);
        }
    }
}
