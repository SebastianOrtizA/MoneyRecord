using MoneyRecord.ViewModels;

namespace MoneyRecord.Views
{
    public partial class CashFlowReportPage : ContentPage
    {
        private readonly CashFlowReportViewModel _viewModel;

        public CashFlowReportPage(CashFlowReportViewModel viewModel)
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
        }
    }
}
