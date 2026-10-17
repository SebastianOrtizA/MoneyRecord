using MoneyRecord.Controls;
using MoneyRecord.ViewModels;

namespace MoneyRecord.Views
{
    public partial class PartyPage : ContentPage, IOverlayHost
    {
        private readonly PartyViewModel _viewModel;

        public PartyPage(PartyViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;
        }

        public bool TryCloseOverlay() => _viewModel.CloseTopOverlay();

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _viewModel.CloseExpenseSheetCommand.Execute(null);
            _viewModel.CloseContributionSheetCommand.Execute(null);
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _viewModel.InitializeAsync();
        }
    }
}
