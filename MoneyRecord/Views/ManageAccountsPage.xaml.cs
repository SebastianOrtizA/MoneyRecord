using MoneyRecord.Controls;
using MoneyRecord.ViewModels;

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
            FormSheet.Panel.SizeChanged += (_, _) => UpdateFormMaxHeight();
            // A new form starts at its top
            FormSheet.Opened += async (_, _) => await FormScroll.ScrollToAsync(0, 0, false);
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

            var chrome = FormSheet.Panel.Height > 0 ? FormSheet.Panel.Height - FormScroll.Height : 0;
            FormScroll.MaximumHeightRequest = Math.Max(0, Height * MaxSheetShare - chrome);
        }

        public bool TryCloseOverlay() => _viewModel.CloseTopOverlay();
    }
}
