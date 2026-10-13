using Microsoft.Extensions.Logging;
using MoneyRecord.Controls;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.ViewModels;

namespace MoneyRecord.Views
{
    /// <summary>
    /// The setup assistant. On a new install it is the window's page until finished, then the Shell replaces it;
    /// from Settings it is pushed and goes back when done. Android back goes to the previous step first.
    /// </summary>
    public partial class SetupPage : ContentPage, IOverlayHost
    {
        private readonly SetupViewModel _viewModel;
        private readonly INavigationService _navigationService;
        private readonly ILogger<SetupPage> _logger;
        private bool _isFirstRun;
        private bool _initialized;

        public SetupPage(SetupViewModel viewModel, INavigationService navigationService, ILogger<SetupPage> logger)
        {
            InitializeComponent();
            _viewModel = viewModel;
            _navigationService = navigationService;
            _logger = logger;
            _viewModel.Completed += OnCompleted;
            // Each step starts at its top
            _viewModel.PropertyChanged += async (_, e) =>
            {
                if (e.PropertyName == nameof(SetupViewModel.Step))
                    await StepScroll.ScrollToAsync(0, 0, false);
            };
            BindingContext = _viewModel;
        }

        /// <summary>Shown before the Shell exists: replaces the seed data and can be skipped.</summary>
        public SetupPage AsFirstRun()
        {
            _isFirstRun = true;
            return this;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            if (_initialized)
                return;
            _initialized = true;
            await _viewModel.InitializeAsync(_isFirstRun);
        }

        private async void OnCompleted(object? sender, EventArgs e)
        {
            if (_isFirstRun)
            {
                Window.Page = new AppShell();
                return;
            }

            // async void: an exception here would crash the app
            try
            {
                await _navigationService.GoBackAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Leaving the setup failed");
            }
        }

        public bool TryCloseOverlay() => _viewModel.TryGoBack();

        // Only reached on the first run: in the Shell, AppShell asks TryCloseOverlay
        protected override bool OnBackButtonPressed() => TryCloseOverlay() || base.OnBackButtonPressed();
    }
}
