using MoneyRecord.Controls;
using MoneyRecord.ViewModels;

namespace MoneyRecord.Views
{
    public partial class BudgetPage : ContentPage, IOverlayHost
    {
        private readonly BudgetViewModel _viewModel;

        public BudgetPage(BudgetViewModel viewModel)
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

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _viewModel.Deactivate();
            // Another tab: come back to the list, not a stale sheet
            _viewModel.CloseFormCommand.Execute(null);
        }

        public bool TryCloseOverlay() => _viewModel.CloseTopOverlay();
    }
}
