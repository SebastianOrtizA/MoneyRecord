using MoneyRecord.ViewModels;

namespace MoneyRecord.Views
{
    public partial class TripPage : ContentPage
    {
        private readonly TripViewModel _viewModel;

        public TripPage(TripViewModel viewModel)
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
