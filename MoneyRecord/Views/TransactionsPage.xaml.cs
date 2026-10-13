using MoneyRecord.Controls;
using MoneyRecord.ViewModels;
using System.ComponentModel;

namespace MoneyRecord.Views
{
    public partial class TransactionsPage : ContentPage, IOverlayHost
    {
        private readonly TransactionsViewModel _viewModel;

        public TransactionsPage(TransactionsViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            BindingContext = _viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            // The load shows its own spinner and errors; the page renders right away
            await _viewModel.InitializeAsync();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            // Another tab or a pushed page: come back to the list, not a stale sheet
            _viewModel.CloseDetailsCommand.Execute(null);
        }

        public bool TryCloseOverlay() => _viewModel.CloseTopOverlay() || AddEntry.TryClose();

        private async void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(TransactionsViewModel.IsDetailsOpen) || !_viewModel.IsDetailsOpen)
                return;

            // Slide the sheet up from below the screen; on the first open it isn't measured yet (Height 0), so start
            // a typical sheet height below
            DetailsSheet.TranslationY = DetailsSheet.Height > 0 ? DetailsSheet.Height : 400;
            await DetailsSheet.TranslateTo(0, 0, 200, Easing.CubicOut);
        }
    }
}
