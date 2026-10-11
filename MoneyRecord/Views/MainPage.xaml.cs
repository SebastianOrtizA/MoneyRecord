using MoneyRecord.Resources.Strings;
using MoneyRecord.ViewModels;

namespace MoneyRecord.Views
{
    public partial class MainPage : ContentPage
    {
        private readonly MainViewModel _viewModel;

        public MainPage(MainViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            // Fire-and-forget pattern for non-blocking UI
            // This allows the page to render immediately while data loads in background
            _ = LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            try
            {
                // Always show loading indicator immediately to prevent UI appearing stuck
                _viewModel.IsRefreshing = true;

                await _viewModel.InitializeAsync();
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync(AppResources.Error, string.Format(AppResources.FailedToLoadData, ex.Message), AppResources.OK);
            }
        }
    }
}
