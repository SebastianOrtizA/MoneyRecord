using MoneyRecord.ViewModels;

namespace MoneyRecord.Views
{
    public partial class NetWorthReportPage : ContentPage
    {
        private readonly NetWorthReportViewModel _viewModel;

        public NetWorthReportPage(NetWorthReportViewModel viewModel)
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
