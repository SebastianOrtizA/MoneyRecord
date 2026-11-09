using MoneyRecord.Controls;
using MoneyRecord.ViewModels;

namespace MoneyRecord.Views
{
    public partial class AccountReportPage : ContentPage, IOverlayHost
    {
        private readonly AccountReportViewModel _viewModel;

        public AccountReportPage(AccountReportViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _viewModel.InitializeAsync();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _viewModel.Deactivate();
            _viewModel.Period.TryCloseSheet();
        }

        public bool TryCloseOverlay() => _viewModel.Period.TryCloseSheet();
    }
}
