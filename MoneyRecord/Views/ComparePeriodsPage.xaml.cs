using MoneyRecord.ViewModels;

namespace MoneyRecord.Views
{
    public partial class ComparePeriodsPage : ContentPage
    {
        private readonly ComparePeriodsViewModel _viewModel;

        public ComparePeriodsPage(ComparePeriodsViewModel viewModel)
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
