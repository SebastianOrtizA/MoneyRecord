using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;
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

        /// <summary>
        /// Back goes to the previous page, then to Home; on Home with no history the app closes.
        /// MainActivity routes the Android back gesture here.
        /// </summary>
        protected override bool OnBackButtonPressed()
        {
            var currentRoute = CurrentState?.Location?.ToString() ?? "";

            if (_navigationService.CanGoBack || !NavigationHistory.IsHome(currentRoute))
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await _navigationService.GoBackAsync();
                });
                return true;
            }

            return base.OnBackButtonPressed();
        }
    }
}
