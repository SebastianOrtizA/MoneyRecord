using MoneyRecord.ViewModels;

namespace MoneyRecord.Views
{
    public partial class FriendsPage : ContentPage
    {
        private readonly FriendsViewModel _viewModel;

        public FriendsPage(FriendsViewModel viewModel)
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
