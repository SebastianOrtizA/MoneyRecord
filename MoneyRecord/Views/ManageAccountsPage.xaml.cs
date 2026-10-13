using MoneyRecord.Controls;
using MoneyRecord.ViewModels;
using System.ComponentModel;

namespace MoneyRecord.Views
{
    public partial class ManageAccountsPage : ContentPage, IOverlayHost
    {
        private readonly ManageAccountsViewModel _viewModel;

        public ManageAccountsPage(ManageAccountsViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
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

        protected override void OnSizeAllocated(double width, double height)
        {
            base.OnSizeAllocated(width, height);
            // The form scrolls inside the sheet (title and buttons stay put); the sheet leaves the top of the list visible
            FormScroll.MaximumHeightRequest = Math.Max(0, height * 0.85 - 200);
        }

        public bool TryCloseOverlay() => _viewModel.CloseTopOverlay();

        private async void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(ManageAccountsViewModel.IsFormOpen) || !_viewModel.IsFormOpen)
                return;

            // Slide the sheet up from below the screen, starting at the top of its form
            await FormScroll.ScrollToAsync(0, 0, false);
            FormSheet.TranslationY = FormSheet.Height > 0 ? FormSheet.Height : 600;
            await FormSheet.TranslateToAsync(0, 0, 200, Easing.CubicOut);
        }
    }
}
