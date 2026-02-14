using MoneyRecord.Services;
using MoneyRecord.Views;

namespace MoneyRecord
{
    public partial class AppShell : Shell
    {
        private readonly INavigationService _navigationService;

        public AppShell()
        {
            InitializeComponent();

            Routing.RegisterRoute(nameof(AddTransactionPage), typeof(AddTransactionPage));
            Routing.RegisterRoute(nameof(AddTransferPage), typeof(AddTransferPage));

            // Get navigation service from DI
            _navigationService = IPlatformApplication.Current?.Services.GetService<INavigationService>()
                ?? new NavigationService();

            // Handle navigation events for tracking history
            this.Navigated += OnShellNavigated;
        }

        private void OnShellNavigated(object? sender, ShellNavigatedEventArgs e)
        {
            var currentRoute = e.Current?.Location?.ToString() ?? "";
            if (!string.IsNullOrEmpty(currentRoute))
            {
                _navigationService.RecordNavigation(currentRoute);
            }
        }

        protected override bool OnBackButtonPressed()
        {
            // If we can go back, navigate to previous page
            if (_navigationService.CanGoBack)
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await _navigationService.GoBackAsync();
                });
                return true;
            }

            // Get the current page route
            var currentRoute = Current?.CurrentState?.Location?.ToString() ?? "";

            // If we're not on the main page, navigate to it
            if (!IsMainPage(currentRoute))
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await GoToAsync("//MainPage");
                });
                return true;
            }

            // On main page with no history, allow default behavior (close app)
            return base.OnBackButtonPressed();
        }

        private static bool IsMainPage(string route)
        {
            return !string.IsNullOrEmpty(route) && 
                (route.EndsWith("MainPage") || route.Contains("//MainPage"));
        }
    }
}
