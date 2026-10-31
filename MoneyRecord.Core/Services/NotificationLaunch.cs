using Microsoft.Extensions.Logging;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Services
{
    /// <summary>
    /// A tapped notification's page (singleton). Android hands it over when the app opens; it is opened at once when the
    /// app is already running, otherwise when Home first appears (the Shell isn't ready before that).
    /// </summary>
    public sealed class NotificationLaunch(INavigationService navigation, ILogger<NotificationLaunch> logger)
    {
        private string? _pending;

        public void Request(string route) => _pending = route;

        /// <summary>Opens the pending page, once; does nothing when there is none. A route that fails is logged, never thrown.</summary>
        public async Task OpenPendingAsync()
        {
            var route = _pending;
            _pending = null;
            if (route == null)
                return;

            // Called from Home's OnAppearing and a main-thread callback: an exception would crash the app
            try
            {
                await navigation.NavigateToAsync(route);
            }
            catch (OperationCanceledException ex)
            {
                logger.LogDebug(ex, "Opening the notification page {Route} was cancelled", route);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Opening the notification page {Route} failed", route);
            }
        }
    }
}
