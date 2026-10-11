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

            // The DI singleton: a private fallback would keep a second history the ViewModels never see
            _navigationService = IPlatformApplication.Current!.Services.GetRequiredService<INavigationService>();

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
                        System.Diagnostics.Debug.WriteLine($"[MoneyRecord] Back failed: {ex}");
                    }
                });
                return true;
            }

            return base.OnBackButtonPressed();
        }
    }
}
