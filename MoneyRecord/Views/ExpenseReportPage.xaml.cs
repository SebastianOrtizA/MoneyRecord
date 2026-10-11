using MoneyRecord.ViewModels;

namespace MoneyRecord.Views
{
    public partial class ExpenseReportPage : ContentPage
    {
        private readonly ExpenseReportViewModel _viewModel;

        public ExpenseReportPage(ExpenseReportViewModel viewModel)
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
