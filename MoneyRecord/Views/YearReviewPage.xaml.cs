using MoneyRecord.ViewModels;

namespace MoneyRecord.Views
{
    public partial class YearReviewPage : ContentPage
    {
        private readonly YearReviewViewModel _viewModel;

        public YearReviewPage(YearReviewViewModel viewModel)
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
