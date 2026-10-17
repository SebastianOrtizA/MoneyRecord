using MoneyRecord.Controls;
using MoneyRecord.ViewModels;

namespace MoneyRecord.Views
{
    public partial class DebtsPage : ContentPage, IOverlayHost
    {
        /// <summary>A sheet's share of the page height; the top of the list stays visible above it.</summary>
        private const double MaxSheetShare = 0.85;

        // The panel's own padding (12 + 20), its handle (4 + 12 margin) and the two 12 px row gaps of a sheet's grid
        private const double SheetChrome = 32 + 16 + 24;

        private readonly DebtsViewModel _viewModel;
        private readonly (ScrollView Scroll, View Header, View Footer)[] _sheets;

        public DebtsPage(DebtsViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            _sheets = [(DetailScroll, DetailHeader, DetailFooter), (FormScroll, FormHeader, FormFooter),
                (PaymentScroll, PaymentHeader, PaymentFooter)];
            foreach (var (scroll, header, footer) in _sheets)
            {
                // From the parts that stay put, never from the scroll area itself (which would feed back into it)
                header.SizeChanged += (_, _) => UpdateMaxHeight(scroll, header, footer);
                footer.SizeChanged += (_, _) => UpdateMaxHeight(scroll, header, footer);
            }
            // A sheet opens at its top
            DetailSheet.Opened += async (_, _) => await DetailScroll.ScrollToAsync(0, 0, false);
            FormSheet.Opened += async (_, _) => await FormScroll.ScrollToAsync(0, 0, false);
            PaymentSheet.Opened += async (_, _) => await PaymentScroll.ScrollToAsync(0, 0, false);
            BindingContext = _viewModel;
        }

        protected override void OnSizeAllocated(double width, double height)
        {
            base.OnSizeAllocated(width, height);
            foreach (var (scroll, header, footer) in _sheets)
                UpdateMaxHeight(scroll, header, footer);
        }

        /// <summary>
        /// The middle of a sheet scrolls while its title and buttons stay put: it gets what is left of
        /// <see cref="MaxSheetShare"/> after their measured height (which grows with the system font size).
        /// </summary>
        private void UpdateMaxHeight(ScrollView scroll, View header, View footer)
        {
            if (Height <= 0)
                return;

            var max = Math.Max(0, Height * MaxSheetShare - SheetChrome - Math.Max(0, header.Height) - Math.Max(0, footer.Height));
            if (Math.Abs(scroll.MaximumHeightRequest - max) >= 1)
                scroll.MaximumHeightRequest = max;
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
            _viewModel.CloseAll();
        }

        public bool TryCloseOverlay() => _viewModel.CloseTopOverlay();

        private void OnLentClicked(object? sender, EventArgs e) => _viewModel.FormIsLent = true;

        private void OnBorrowedClicked(object? sender, EventArgs e) => _viewModel.FormIsLent = false;
    }
}
