namespace MoneyRecord.Services.Interfaces
{
    /// <summary>
    /// Shell navigation for ViewModels, with a history for the back button.
    /// </summary>
    public interface INavigationService
    {
        bool CanGoBack { get; }

        /// <summary>Records where Shell ended up. AppShell calls this from its <c>Navigated</c> event.</summary>
        void RecordNavigation(string location);

        /// <summary>Returns to the previous page (Home when there is none).</summary>
        Task GoBackAsync();

        Task NavigateToAsync(string route);
        Task NavigateToAsync(string route, IDictionary<string, object> parameters);
    }
}
