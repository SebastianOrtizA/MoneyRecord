using System.Windows.Input;

namespace MoneyRecord.Controls
{
    /// <summary>
    /// An in-page bottom sheet: a dimmed page behind a rounded panel with a handle that slides up while
    /// <see cref="IsOpen"/>. Tapping the dim runs <see cref="CloseCommand"/>; the view model owns the open state.
    /// Place it last in the page's root grid (spanning its rows) so it covers the page.
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

        public static readonly BindableProperty SheetPaddingProperty =
            BindableProperty.Create(nameof(SheetPadding), typeof(Thickness), typeof(BottomSheet), new Thickness(20, 12, 20, 20),
                propertyChanged: (bindable, _, newValue) => ((BottomSheet)bindable).Panel.Padding = (Thickness)newValue);

        private readonly ContentView _contentHost = new();

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

            var handle = new Border
            {
                WidthRequest = 36,
                HeightRequest = 4,
                StrokeThickness = 0,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 2 },
                HorizontalOptions = LayoutOptions.Center,
                Margin = new Thickness(0, 0, 0, 12)
            };
            handle.SetAppThemeColor(BackgroundColorProperty, Color.FromArgb("#BDBDBD"), Color.FromArgb("#616161"));

            Panel = new Border
            {
                VerticalOptions = LayoutOptions.End,
                Padding = SheetPadding,
                StrokeThickness = 0,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(20, 20, 0, 0) },
                Content = new VerticalStackLayout { Children = { handle, _contentHost } }
            };
            Panel.SetAppThemeColor(BackgroundColorProperty, Colors.White, Color.FromArgb("#1E1E1E"));

            Children.Add(scrim);
            Children.Add(Panel);
        }

        /// <summary>The rounded panel (for measuring it, e.g. to cap a scrolling form inside).</summary>
        public Border Panel { get; }

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

        public Thickness SheetPadding
        {
            get => (Thickness)GetValue(SheetPaddingProperty);
            set => SetValue(SheetPaddingProperty, value);
        }

        /// <summary>Raised when the sheet opens, before it slides up (e.g. to scroll its form back to the top).</summary>
        public event EventHandler? Opened;

        private async void OnIsOpenChanged(bool isOpen)
        {
            if (!isOpen)
            {
                IsVisible = false;
                return;
            }

            // Below the screen before it becomes visible, so it is never drawn in place first. On the first open it
            // isn't measured yet (Height 0), so it starts a typical sheet height below
            Panel.TranslationY = Panel.Height > 0 ? Panel.Height : 600;
            IsVisible = true;
            Opened?.Invoke(this, EventArgs.Empty);
            await Panel.TranslateToAsync(0, 0, 200, Easing.CubicOut);
        }
    }
}
