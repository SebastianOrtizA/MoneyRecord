using MoneyRecord.Controls;
using MoneyRecord.ViewModels;

namespace MoneyRecord.Views
{
    public partial class QuickEntriesPage : ContentPage, IOverlayHost
    {
        private readonly QuickEntriesViewModel _viewModel;

        public QuickEntriesPage(QuickEntriesViewModel viewModel)
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
            _viewModel.CloseForm();
        }

        public bool TryCloseOverlay() => _viewModel.CloseTopOverlay();
    }
}
