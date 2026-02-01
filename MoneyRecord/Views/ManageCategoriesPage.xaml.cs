using MoneyRecord.Models;
using MoneyRecord.ViewModels;

namespace MoneyRecord.Views
{
    public partial class ManageCategoriesPage : ContentPage
    {
        private readonly ManageCategoriesViewModel _viewModel;

        public ManageCategoriesPage(ManageCategoriesViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;
        }

        protected override async void OnNavigatedTo(NavigatedToEventArgs args)
        {
            base.OnNavigatedTo(args);

            // Detect route and set CategoryType accordingly
            var currentRoute = Shell.Current.CurrentState.Location.ToString();
            
            if (currentRoute.Contains("ManageExpenseCategories"))
            {
                _viewModel.CategoryType = CategoryType.Expense;
            }
            else if (currentRoute.Contains("ManageIncomeCategories"))
            {
                _viewModel.CategoryType = CategoryType.Income;
            }
            
            await _viewModel.InitializeAsync();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            
            // Only initialize if CategoryType hasn't been set yet (navigated from MainPage)
            if (_viewModel.CategoryType == default)
            {
                await _viewModel.InitializeAsync();
            }
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
