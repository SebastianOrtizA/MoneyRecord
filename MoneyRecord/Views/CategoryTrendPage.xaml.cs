using MoneyRecord.ViewModels;

namespace MoneyRecord.Views
{
    public partial class CategoryTrendPage : ContentPage
    {
        private readonly CategoryTrendViewModel _viewModel;

        public CategoryTrendPage(CategoryTrendViewModel viewModel)
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
