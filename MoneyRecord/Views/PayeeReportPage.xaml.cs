using MoneyRecord.Controls;
using MoneyRecord.ViewModels;

namespace MoneyRecord.Views
{
    public partial class PayeeReportPage : ContentPage, IOverlayHost
    {
        private readonly PayeeReportViewModel _viewModel;

        public PayeeReportPage(PayeeReportViewModel viewModel)
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
