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

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _viewModel.InitializeAsync();
        }
    }
}
