using MoneyRecord.ViewModels;

namespace MoneyRecord.Views
{
    public partial class ChallengePage : ContentPage
    {
        private readonly ChallengeViewModel _viewModel;

        public ChallengePage(ChallengeViewModel viewModel)
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
