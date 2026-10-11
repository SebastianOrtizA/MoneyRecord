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

        public bool CanGoBack => _history.CanGoBack;

        public void RecordNavigation(string location) => _history.Record(location);

        public Task GoBackAsync() => Shell.Current.GoToAsync(_history.BeginGoBack());

        public Task NavigateToAsync(string route) => Shell.Current.GoToAsync(route);

        public Task NavigateToAsync(string route, IDictionary<string, object> parameters) =>
            Shell.Current.GoToAsync(route, parameters);
    }
}
