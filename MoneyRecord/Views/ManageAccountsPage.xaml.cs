using MoneyRecord.ViewModels;

namespace MoneyRecord.Views
{
    public partial class ManageAccountsPage : ContentPage
    {
        private readonly ManageAccountsViewModel _viewModel;

        public ManageAccountsPage(ManageAccountsViewModel viewModel)
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
