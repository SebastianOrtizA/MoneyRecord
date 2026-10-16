using MoneyRecord.ViewModels;

namespace MoneyRecord.Views
{
    public partial class PayeesPage : ContentPage
    {
        private readonly PayeesViewModel _viewModel;

        public PayeesPage(PayeesViewModel viewModel)
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
