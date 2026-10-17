using MoneyRecord.Controls;
using MoneyRecord.ViewModels;

namespace MoneyRecord.Views
{
    public partial class TripsPage : ContentPage, IOverlayHost
    {
        private readonly TripsViewModel _viewModel;

        public TripsPage(TripsViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _viewModel.InitializeAsync();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            // Back here later: the list, not a stale sheet
            _viewModel.CloseFormCommand.Execute(null);
        }

        public bool TryCloseOverlay() => _viewModel.CloseTopOverlay();
    }
}
