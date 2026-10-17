using MoneyRecord.Controls;
using MoneyRecord.ViewModels;

namespace MoneyRecord.Views
{
    public partial class DebtsPage : ContentPage, IOverlayHost
    {
        private readonly DebtsViewModel _viewModel;

        public DebtsPage(DebtsViewModel viewModel)
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
            _viewModel.CloseAll();
        }

        public bool TryCloseOverlay() => _viewModel.CloseTopOverlay();

        private void OnLentClicked(object? sender, EventArgs e) => _viewModel.FormIsLent = true;

        private void OnBorrowedClicked(object? sender, EventArgs e) => _viewModel.FormIsLent = false;
    }
}
