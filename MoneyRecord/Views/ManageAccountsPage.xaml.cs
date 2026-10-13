using MoneyRecord.Controls;
using MoneyRecord.ViewModels;
using System.ComponentModel;

namespace MoneyRecord.Views
{
    public partial class ManageAccountsPage : ContentPage, IOverlayHost
    {
        /// <summary>The sheet's share of the page height; the top of the list stays visible above it.</summary>
        private const double MaxSheetShare = 0.85;

        private readonly ManageAccountsViewModel _viewModel;

        public ManageAccountsPage(ManageAccountsViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            FormSheet.SizeChanged += (_, _) => UpdateFormMaxHeight();
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
            _viewModel.CloseFormCommand.Execute(null);
        }

        protected override void OnSizeAllocated(double width, double height)
        {
            base.OnSizeAllocated(width, height);
            UpdateFormMaxHeight();
        }

        /// <summary>
        /// The form scrolls inside the sheet while the handle, title and buttons stay put: it gets what is left of
        /// <see cref="MaxSheetShare"/> after their measured height (which grows with the system font size).
        /// </summary>
        private void UpdateFormMaxHeight()
        {
            if (Height <= 0)
                return;

            var chrome = FormSheet.Height > 0 ? FormSheet.Height - FormScroll.Height : 0;
            FormScroll.MaximumHeightRequest = Math.Max(0, Height * MaxSheetShare - chrome);
        }

        public bool TryCloseOverlay() => _viewModel.CloseTopOverlay();

        private async void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(ManageAccountsViewModel.IsFormOpen) || !_viewModel.IsFormOpen)
                return;

            // Below the screen before the first await, so the sheet is never drawn in place first; the form starts at the top
            var slideUp = FormSheet.SlideUpAsync(600);
            await FormScroll.ScrollToAsync(0, 0, false);
            await slideUp;
        }
    }
}
