namespace MoneyRecord.Services
{
    /// <summary>
    /// Back-button history over Shell locations. A flyout page (<c>//Route</c>) is a root and clears the history;
    /// a pushed page (<c>//Route/Page</c>) remembers where it came from.
    /// </summary>
    public sealed class NavigationHistory
    {
        public const string HomeRoute = "//MainPage";

        private readonly Stack<string> _history = new();
        private bool _isNavigatingBack;

        public string? CurrentLocation { get; private set; }

        public bool CanGoBack => _history.Count > 0;

        /// <summary>Records the location Shell navigated to.</summary>
        public void Record(string location)
        {
            if (_isNavigatingBack)
            {
                _isNavigatingBack = false;
            }
            else if (IsRoot(location))
            {
                _history.Clear();
            }
            else if (!string.IsNullOrEmpty(CurrentLocation))
            {
                _history.Push(CurrentLocation);
            }

            CurrentLocation = location;
        }

        /// <summary>
        /// Pops the history and returns the route to hand to Shell: <c>..</c> to leave a pushed page,
        /// otherwise the previous location (Home when there is none). Returns null while a previous go-back
        /// has not reached <see cref="Record"/> yet, so a fast double back press pops only once.
        /// </summary>
        public string? BeginGoBack()
        {
            if (_isNavigatingBack)
                return null;

            _isNavigatingBack = true;
            var previous = _history.Count > 0 ? _history.Pop() : HomeRoute;
            return CurrentLocation != null && !IsRoot(CurrentLocation) ? ".." : previous;
        }

        /// <summary>Call when the Shell navigation started by <see cref="BeginGoBack"/> failed.</summary>
        public void CancelGoBack() => _isNavigatingBack = false;

        /// <summary>True for a flyout page: an absolute location with one segment, such as <c>//BudgetPage</c>.</summary>
        public static bool IsRoot(string location)
        {
            if (!location.StartsWith("//", StringComparison.Ordinal) || location.Length == 2)
                return false;

            var path = location.AsSpan(2);
            var query = path.IndexOf('?');
            if (query >= 0) path = path[..query];
            return path.IndexOf('/') < 0;
        }

        public static bool IsHome(string location) =>
            string.Equals(location, HomeRoute, StringComparison.OrdinalIgnoreCase);
    }
}
