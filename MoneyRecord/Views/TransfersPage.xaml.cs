using MoneyRecord.ViewModels;

namespace MoneyRecord.Views
{
    public partial class TransfersPage : ContentPage
    {
        private readonly TransfersViewModel _viewModel;

        public TransfersPage(TransfersViewModel viewModel)
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
