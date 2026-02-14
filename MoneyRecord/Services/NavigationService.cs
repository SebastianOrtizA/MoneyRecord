using System.Collections.Generic;

namespace MoneyRecord.Services
{
    /// <summary>
    /// Service to manage navigation history and provide proper back navigation.
    /// Implements a simple stack-based history for tracking visited screens.
    /// </summary>
    public class NavigationService : INavigationService
    {
        private readonly Stack<string> _navigationHistory = new();
        private string? _currentRoute;
        private bool _isNavigatingBack;

        // Routes that should be the root and clear history when navigated to
        private static readonly HashSet<string> RootRoutes = new(StringComparer.OrdinalIgnoreCase)
        {
            "//MainPage",
            "//ExpenseReportPage",
            "//IncomeReportPage",
            "//TransfersPage",
            "//ManageAccounts",
            "//ManageIncomeCategories",
            "//ManageExpenseCategories"
        };

        public string? CurrentRoute => _currentRoute;
        
        public bool CanGoBack => _navigationHistory.Count > 0;

        /// <summary>
        /// Records a navigation event. Call this when navigating to a new page.
        /// </summary>
        /// <param name="route">The route being navigated to</param>
        public void RecordNavigation(string route)
        {
            if (_isNavigatingBack)
            {
                _isNavigatingBack = false;
                _currentRoute = route;
                return;
            }

            // If navigating to a root route, clear history
            if (IsRootRoute(route))
            {
                _navigationHistory.Clear();
                _currentRoute = route;
                return;
            }

            // Push current route to history before navigating (if we have one)
            if (!string.IsNullOrEmpty(_currentRoute))
            {
                _navigationHistory.Push(_currentRoute);
            }

            _currentRoute = route;
        }

        /// <summary>
        /// Gets the previous route to navigate back to.
        /// </summary>
        /// <returns>The route to navigate back to, or MainPage if no history</returns>
        public string GetBackRoute()
        {
            if (_navigationHistory.Count > 0)
            {
                return _navigationHistory.Pop();
            }

            // Default to MainPage if no history
            return "//MainPage";
        }

        /// <summary>
        /// Navigates back to the previous page.
        /// </summary>
        public async Task GoBackAsync()
        {
            _isNavigatingBack = true;
            var backRoute = GetBackRoute();
            
            // Determine if we need absolute or relative navigation
            if (backRoute.StartsWith("//"))
            {
                await Shell.Current.GoToAsync(backRoute);
            }
            else
            {
                // For modal/pushed pages, use relative navigation
                await Shell.Current.GoToAsync("..");
            }
        }

        /// <summary>
        /// Clears the navigation history.
        /// </summary>
        public void ClearHistory()
        {
            _navigationHistory.Clear();
            _currentRoute = null;
        }

        private static bool IsRootRoute(string route)
        {
            // Normalize route for comparison
            var normalizedRoute = route.StartsWith("//") ? route : $"//{route}";
            return RootRoutes.Contains(normalizedRoute);
        }
    }

    /// <summary>
    /// Interface for navigation service to support dependency injection and testing.
    /// </summary>
    public interface INavigationService
    {
        string? CurrentRoute { get; }
        bool CanGoBack { get; }
        void RecordNavigation(string route);
        string GetBackRoute();
        Task GoBackAsync();
        void ClearHistory();
    }
}
