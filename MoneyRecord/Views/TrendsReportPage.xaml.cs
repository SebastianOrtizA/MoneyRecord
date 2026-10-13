using MoneyRecord.ViewModels;

namespace MoneyRecord.Views
{
    public partial class TrendsReportPage : ContentPage
    {
        private readonly TrendsReportViewModel _viewModel;

        public TrendsReportPage(TrendsReportViewModel viewModel)
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
    }
}
