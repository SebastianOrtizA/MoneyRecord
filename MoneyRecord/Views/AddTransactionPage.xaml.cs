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

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _viewModel.InitializeAsync();
        }
    }
}
