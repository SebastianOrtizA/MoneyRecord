using MoneyRecord.ViewModels;

namespace MoneyRecord.Views
{
    public partial class ForecastPage : ContentPage
    {
        private readonly ForecastChartViewModel _viewModel;

        public ForecastPage(ForecastChartViewModel viewModel)
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
