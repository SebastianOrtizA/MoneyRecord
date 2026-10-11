using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Services
{
    /// <summary>
    /// Shell navigation with a back-button history (<see cref="NavigationHistory"/>).
    /// Only AppShell's Navigated event feeds the history, so each navigation is recorded once.
    /// </summary>
    public class NavigationService : INavigationService
    {
        private readonly NavigationHistory _history = new();

        public void RecordNavigation(string location) => _history.Record(location);

        public async Task GoBackAsync()
        {
            // Null: a go-back is already in progress (back pressed twice quickly)
            var route = _history.BeginGoBack();
            if (route == null)
                return;

            try
            {
                await Shell.Current.GoToAsync(route);
            }
            catch
            {
                // Navigated won't fire, so the next navigation must still be recorded
                _history.CancelGoBack();
                throw;
            }
        }

        public Task NavigateToAsync(string route) => Shell.Current.GoToAsync(route);

        public Task NavigateToAsync(string route, IDictionary<string, object> parameters) =>
            Shell.Current.GoToAsync(route, parameters);
    }
}
