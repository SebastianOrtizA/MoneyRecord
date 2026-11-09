using MoneyRecord.Controls;
using MoneyRecord.ViewModels;

namespace MoneyRecord.Views
{
    public partial class TagsPage : ContentPage, IOverlayHost
    {
        private readonly TagsViewModel _viewModel;

        public TagsPage(TagsViewModel viewModel)
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

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _viewModel.Deactivate();
            _viewModel.Period.TryCloseSheet();
        }

        public bool TryCloseOverlay() => _viewModel.Period.TryCloseSheet();
    }
}
