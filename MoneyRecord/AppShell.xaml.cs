using Microsoft.Extensions.Logging;
using MoneyRecord.Controls;
using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.Views;

namespace MoneyRecord
{
    public partial class AppShell : Shell
    {
        private readonly INavigationService _navigationService;
        private readonly ILogger<AppShell> _logger;

        public AppShell()
        {
            InitializeComponent();

            Routing.RegisterRoute(nameof(AddTransactionPage), typeof(AddTransactionPage));
            Routing.RegisterRoute(nameof(AddTransferPage), typeof(AddTransferPage));

            // Opened from the More tab
            Routing.RegisterRoute(nameof(ManageAccountsPage), typeof(ManageAccountsPage));
            Routing.RegisterRoute(nameof(ManageCategoriesPage), typeof(ManageCategoriesPage));
            Routing.RegisterRoute(nameof(RecurringTransactionsPage), typeof(RecurringTransactionsPage));
            Routing.RegisterRoute(nameof(TransfersPage), typeof(TransfersPage));
            Routing.RegisterRoute(nameof(SettingsPage), typeof(SettingsPage));

            // The DI singleton: a private fallback would keep a second history the ViewModels never see
            var services = IPlatformApplication.Current!.Services;
            _navigationService = services.GetRequiredService<INavigationService>();
            _logger = services.GetRequiredService<ILogger<AppShell>>();

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
        /// Back closes an open sheet first (<see cref="IOverlayHost"/>), then goes to the previous page, then to Home;
        /// on Home with no history the app closes.
        /// MainActivity routes the Android back gesture here.
        /// </summary>
        protected override bool OnBackButtonPressed()
        {
            if (CurrentPage is IOverlayHost host && host.TryCloseOverlay())
                return true;

            var currentRoute = CurrentState?.Location?.ToString() ?? "";

            // Home is a root, so its history is always empty: anywhere else, back has somewhere to go
            if (!NavigationHistory.IsHome(currentRoute))
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    // async void: an exception here would crash the app
                    try
                    {
                        await _navigationService.GoBackAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Back navigation failed");
                    }
                });
                return true;
            }

            return base.OnBackButtonPressed();
        }
    }
}
