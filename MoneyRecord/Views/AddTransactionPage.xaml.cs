using MoneyRecord.ViewModels;

namespace MoneyRecord.Views
{
    public partial class AddTransactionPage : ContentPage
    {
        private readonly AddTransactionViewModel _viewModel;

        public AddTransactionPage(AddTransactionViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            // Fire-and-forget for non-blocking UI
            _ = _viewModel.InitializeAsync();
        }

        protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
        {
            base.OnNavigatedFrom(args);
            // Back button / gesture without saving (not raised while the camera or gallery is open)
            _viewModel.DiscardUnsavedPhoto();
        }
    }
}
