using MoneyRecord.Controls;
using MoneyRecord.ViewModels;

namespace MoneyRecord.Views
{
    public partial class TransactionsPage : ContentPage, IOverlayHost
    {
        private readonly TransactionsViewModel _viewModel;

        public TransactionsPage(TransactionsViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            // The load shows its own spinner and errors; the page renders right away
            await _viewModel.InitializeAsync();
        }

        private void OnSwipeStarted(object? sender, SwipeStartedEventArgs e) => _viewModel.MarkSwipeSeen();

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _viewModel.Deactivate();
            // Another tab or a pushed page: come back to the list, not a stale sheet
            _viewModel.CloseDetailsCommand.Execute(null);
        }

        public bool TryCloseOverlay() => _viewModel.CloseTopOverlay() || AddEntry.TryClose();
    }
}
