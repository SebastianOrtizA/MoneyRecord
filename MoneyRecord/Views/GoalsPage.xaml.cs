using MoneyRecord.Controls;
using MoneyRecord.ViewModels;

namespace MoneyRecord.Views
{
    public partial class GoalsPage : ContentPage, IOverlayHost
    {
        private readonly GoalsViewModel _viewModel;

        public GoalsPage(GoalsViewModel viewModel)
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
            // Back here later: the list, not a stale sheet
            _viewModel.CloseFormCommand.Execute(null);
        }

        public bool TryCloseOverlay() => _viewModel.CloseTopOverlay();

        // The whole row ticks its box, not only the small box itself
        private void OnAccountRowTapped(object? sender, TappedEventArgs e)
        {
            if ((sender as BindableObject)?.BindingContext is GoalAccountChoice choice)
                choice.IsSelected = !choice.IsSelected;
        }
    }
}
