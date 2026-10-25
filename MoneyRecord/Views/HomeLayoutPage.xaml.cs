using MoneyRecord.ViewModels;

namespace MoneyRecord.Views
{
    public partial class HomeLayoutPage : ContentPage
    {
        private readonly HomeLayoutViewModel _viewModel;

        public HomeLayoutPage(HomeLayoutViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            _viewModel.Initialize();
        }
    }
}
