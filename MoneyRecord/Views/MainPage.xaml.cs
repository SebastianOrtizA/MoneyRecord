using MoneyRecord.Controls;
using MoneyRecord.ViewModels;

namespace MoneyRecord.Views
{
    public partial class MainPage : ContentPage, IOverlayHost
    {
        private readonly MainViewModel _viewModel;

        public MainPage(MainViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            // The load shows its own spinner and errors; the page renders right away
            await _viewModel.InitializeAsync();
        }

        public bool TryCloseOverlay() => AddEntry.TryClose();
    }
}
