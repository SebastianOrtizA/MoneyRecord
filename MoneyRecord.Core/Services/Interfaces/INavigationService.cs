namespace MoneyRecord.Services.Interfaces
{
    /// <summary>
    /// Shell navigation for ViewModels, with a history for the back button.
    /// </summary>
    public interface INavigationService
    {
        /// <summary>Records where Shell ended up. AppShell calls this from its <c>Navigated</c> event.</summary>
        void RecordNavigation(string location);

        /// <summary>Returns to the previous page (Home when there is none).</summary>
        Task GoBackAsync();

        Task NavigateToAsync(string route);

        /// <summary>
        /// Goes to a tab page but remembers the current page, so back returns here (a report opening its transactions)
        /// instead of going Home.
        /// </summary>
        Task NavigateToKeepingBackAsync(string route);

        Task NavigateToAsync(string route, IDictionary<string, object> parameters);

        /// <summary>Swaps the current pushed page for another one (the add form's type switch), so back skips the old page.</summary>
        Task ReplaceAsync(string route, IDictionary<string, object> parameters);
    }
}
