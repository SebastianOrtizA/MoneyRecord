using MoneyRecord.Controls;
using MoneyRecord.ViewModels;

namespace MoneyRecord.Views
{
    public partial class TripCategoriesPage : ContentPage, IOverlayHost
    {
        private readonly TripCategoriesViewModel _viewModel;

        public TripCategoriesPage(TripCategoriesViewModel viewModel)
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
            _viewModel.CloseFormCommand.Execute(null);
        }

        public bool TryCloseOverlay() => _viewModel.CloseTopOverlay();
    }
}
