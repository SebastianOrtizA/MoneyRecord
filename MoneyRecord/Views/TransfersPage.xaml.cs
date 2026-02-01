using MoneyRecord.ViewModels;

namespace MoneyRecord.Views
{
    public partial class TransfersPage : ContentPage
    {
        private readonly TransfersViewModel _viewModel;

        public TransfersPage(TransfersViewModel viewModel)
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
