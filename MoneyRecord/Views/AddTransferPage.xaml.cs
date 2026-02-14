using MoneyRecord.ViewModels;

namespace MoneyRecord.Views
{
    public partial class AddTransferPage : ContentPage
    {
        private readonly AddTransferViewModel _viewModel;

        public AddTransferPage(AddTransferViewModel viewModel)
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
    }
}
