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

        protected override bool OnBackButtonPressed()
        {
            // Navigate to main page instead of closing the app
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await Shell.Current.GoToAsync("//MainPage");
            });
            return true; // Indicate we've handled the back button
        }
    }
}
