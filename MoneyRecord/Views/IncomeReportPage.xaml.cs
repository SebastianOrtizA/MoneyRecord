using MoneyRecord.ViewModels;

namespace MoneyRecord.Views
{
    public partial class IncomeReportPage : ContentPage
    {
        private readonly IncomeReportViewModel _viewModel;

        public IncomeReportPage(IncomeReportViewModel viewModel)
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
    }
}
