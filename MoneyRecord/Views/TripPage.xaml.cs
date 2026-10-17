using MoneyRecord.Controls;
using MoneyRecord.ViewModels;

namespace MoneyRecord.Views
{
    public partial class TripPage : ContentPage, IOverlayHost
    {
        private readonly TripViewModel _viewModel;

        public TripPage(TripViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;
        }

        public bool TryCloseOverlay() => _viewModel.CloseTopOverlay();

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _viewModel.CloseEndSheetCommand.Execute(null);
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _viewModel.InitializeAsync();
        }
    }
}
