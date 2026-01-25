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

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _viewModel.InitializeAsync();
        }
    }
}
