using MoneyRecord.Controls;
using MoneyRecord.ViewModels;

namespace MoneyRecord.Views
{
    public partial class ManageAccountsPage : ContentPage, IOverlayHost
    {
        private readonly ManageAccountsViewModel _viewModel;

        public ManageAccountsPage(ManageAccountsViewModel viewModel)
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
            // The currency list or the conversion question covers this page; the form they were opened from stays
            if (!CurrencyPicker.IsPicking && !_viewModel.IsAskingConversion)
                _viewModel.CloseAll();
        }

        public bool TryCloseOverlay() => _viewModel.CloseTopOverlay();
    }
}
