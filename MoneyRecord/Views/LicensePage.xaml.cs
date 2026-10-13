using MoneyRecord.ViewModels;

namespace MoneyRecord.Views
{
    public partial class LicensePage : ContentPage
    {
        private readonly LicenseViewModel _viewModel;

        public LicensePage(LicenseViewModel viewModel)
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
    }
}
