using System.Windows.Input;

namespace MoneyRecord.Controls
{
    /// <summary>
    /// An in-page bottom sheet: a dimmed page behind a rounded panel with a handle that slides up while
    /// <see cref="IsOpen"/>. Tapping the dim runs <see cref="CloseCommand"/>; the view model owns the open state.
    /// Place it last in the page's root grid (spanning its rows) so it covers the page.
    /// A long form names its middle part as <see cref="Scroll"/>: that part scrolls while its title and buttons stay put.
    /// </summary>
    [ContentProperty(nameof(SheetContent))]
    public class BottomSheet : Grid
    {
        public static readonly BindableProperty IsOpenProperty =
            BindableProperty.Create(nameof(IsOpen), typeof(bool), typeof(BottomSheet), false,
                propertyChanged: (bindable, _, newValue) => ((BottomSheet)bindable).OnIsOpenChanged((bool)newValue));

        public static readonly BindableProperty CloseCommandProperty =
            BindableProperty.Create(nameof(CloseCommand), typeof(ICommand), typeof(BottomSheet));

        public static readonly BindableProperty SheetContentProperty =
            BindableProperty.Create(nameof(SheetContent), typeof(View), typeof(BottomSheet),
                propertyChanged: (bindable, _, newValue) => ((BottomSheet)bindable)._contentHost.Content = (View?)newValue);

        public static readonly BindableProperty ScrollProperty =
            BindableProperty.Create(nameof(Scroll), typeof(ScrollView), typeof(BottomSheet),
                propertyChanged: (bindable, _, newValue) => ((BottomSheet)bindable).OnScrollChanged((ScrollView?)newValue));

        public static readonly BindableProperty MaxShareProperty =
            BindableProperty.Create(nameof(MaxShare), typeof(double), typeof(BottomSheet), 0.85,
                propertyChanged: (bindable, _, _) => ((BottomSheet)bindable).UpdateScrollCap());

        private readonly ContentView _contentHost = new();
        private readonly Border _handle;
        private readonly Border _panel;

        public BottomSheet()
        {
            IsVisible = false;

            var scrim = new BoxView { BackgroundColor = Color.FromArgb("#80000000") };
            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) =>
            {
                if (CloseCommand?.CanExecute(null) == true)
                    CloseCommand.Execute(null);
            };
            scrim.GestureRecognizers.Add(tap);

            _handle = new Border
            {
                WidthRequest = 36,
                HeightRequest = 4,
                StrokeThickness = 0,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 2 },
                HorizontalOptions = LayoutOptions.Center,
                Margin = new Thickness(0, 0, 0, 12)
            };
            _handle.SetAppThemeColor(BackgroundColorProperty, Color.FromArgb("#BDBDBD"), Color.FromArgb("#616161"));

            _panel = new Border
            {
                VerticalOptions = LayoutOptions.End,
                Padding = new Thickness(20, 12, 20, 20),
                StrokeThickness = 0,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(20, 20, 0, 0) },
                Content = new VerticalStackLayout { Children = { _handle, _contentHost } }
            };
            _panel.SetAppThemeColor(BackgroundColorProperty, Colors.White, Color.FromArgb("#1E1E1E"));

            Children.Add(scrim);
            Children.Add(_panel);

            SizeChanged += (_, _) => UpdateScrollCap();
        }

        public bool IsOpen
        {
            get => (bool)GetValue(IsOpenProperty);
            set => SetValue(IsOpenProperty, value);
        }

        /// <summary>Run when the dim behind the sheet is tapped.</summary>
        public ICommand? CloseCommand
        {
            get => (ICommand?)GetValue(CloseCommandProperty);
            set => SetValue(CloseCommandProperty, value);
        }

        /// <summary>What the sheet shows below its handle.</summary>
        public View? SheetContent
        {
            get => (View?)GetValue(SheetContentProperty);
            set => SetValue(SheetContentProperty, value);
        }

        /// <summary>
        /// The part of the content that scrolls (<c>Scroll="{x:Reference FormScroll}"</c>): capped so the whole sheet takes
        /// at most <see cref="MaxShare"/> of the page height, and back at its top whenever the sheet opens.
        /// </summary>
        public ScrollView? Scroll
        {
            get => (ScrollView?)GetValue(ScrollProperty);
            set => SetValue(ScrollProperty, value);
        }

        /// <summary>The sheet's largest share of the page height; the top of the page stays visible above it.</summary>
        public double MaxShare
        {
            get => (double)GetValue(MaxShareProperty);
            set => SetValue(MaxShareProperty, value);
        }

        private void OnScrollChanged(ScrollView? scroll)
        {
            if (scroll == null)
                return;

            // Its siblings (title, buttons) are known once it is in the content
            if (scroll.Parent is Layout)
                WatchSiblings(scroll);
            else
                scroll.ParentChanged += (_, _) => WatchSiblings(scroll);
        }

        private void WatchSiblings(ScrollView scroll)
        {
            if (scroll.Parent is not Layout content)
                return;

            foreach (var sibling in content.Children.OfType<View>().Where(v => v != scroll))
                sibling.SizeChanged += (_, _) => UpdateScrollCap();
            UpdateScrollCap();
        }

        /// <summary>
        /// The scroll gets what is left of <see cref="MaxShare"/> after the parts that stay put: the panel's padding, the
        /// handle, the scroll's siblings and the gaps between them (all grow with the system font size). Never computed
        /// from the scroll's own height, which would feed back into it.
        /// </summary>
        private void UpdateScrollCap()
        {
            if (Height <= 0 || Scroll is not { Parent: Layout content } scroll)
                return;

            var fixedHeight = _panel.Padding.VerticalThickness + _handle.HeightRequest + _handle.Margin.VerticalThickness;
            var parts = 0;
            foreach (var view in content.Children.OfType<View>().Where(v => v.IsVisible))
            {
                parts++;
                if (view != scroll)
                    fixedHeight += Math.Max(0, view.Height);
            }
            var gap = content switch
            {
                Grid grid => grid.RowSpacing,
                StackBase stack => stack.Spacing,
                _ => 0
            };
            fixedHeight += gap * Math.Max(0, parts - 1);

            var max = Math.Max(0, Height * MaxShare - fixedHeight);
            if (Math.Abs(scroll.MaximumHeightRequest - max) >= 1)
                scroll.MaximumHeightRequest = max;
        }

        private async void OnIsOpenChanged(bool isOpen)
        {
            if (!isOpen)
            {
                IsVisible = false;
                return;
            }

            // Below the screen before it becomes visible, so it is never drawn in place first. On the first open it
            // isn't measured yet (Height 0), so it starts a typical sheet height below
            _panel.TranslationY = _panel.Height > 0 ? _panel.Height : 600;
            IsVisible = true;
            // A form opens at its top
            if (Scroll != null)
                await Scroll.ScrollToAsync(0, 0, false);
            await _panel.TranslateToAsync(0, 0, 200, Easing.CubicOut);
        }
    }
}
