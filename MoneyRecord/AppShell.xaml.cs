using MoneyRecord.Views;

namespace MoneyRecord
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            Routing.RegisterRoute(nameof(AddTransactionPage), typeof(AddTransactionPage));
            Routing.RegisterRoute(nameof(AddTransferPage), typeof(AddTransferPage));

            // Handle navigation events for back button behavior
            this.Navigating += OnShellNavigating;
        }

        private void OnShellNavigating(object? sender, ShellNavigatingEventArgs e)
        {
            // If navigating back and would result in app closing, redirect to main page
            if (e.Source == ShellNavigationSource.Pop || e.Source == ShellNavigationSource.PopToRoot)
            {
                var currentRoute = Current?.CurrentState?.Location?.ToString() ?? "";
                
                // If not on main page and trying to go back, redirect to main page
                if (!IsMainPage(currentRoute))
                {
                    e.Cancel();
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        await GoToAsync("//MainPage");
                    });
                }
            }
        }

        private static bool IsMainPage(string route)
        {
            return !string.IsNullOrEmpty(route) && 
                (route.EndsWith("MainPage") || route.Contains("//MainPage"));
        }

        protected override bool OnBackButtonPressed()
        {
            // Get the current page route
            var currentRoute = Current?.CurrentState?.Location?.ToString() ?? "";

            // If we're not on the main page, navigate to it
            if (!IsMainPage(currentRoute))
            {
                // Navigate to main page
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await GoToAsync("//MainPage");
                });
                return true; // Indicate we've handled the back button
            }

            // On main page, allow default behavior (close app)
            return base.OnBackButtonPressed();
        }
    }
}
