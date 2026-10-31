namespace MoneyRecord.Controls
{
    /// <summary>
    /// A rounded 8 dp bar for a budget: filled to <see cref="Used"/> of the limit, and once spending passes it the bar is
    /// full with a notch where the limit was, so 216% doesn't look like 100%.
    /// </summary>
    public class BudgetBar : ContentView
    {
        public static readonly BindableProperty UsedProperty = BindableProperty.Create(nameof(Used), typeof(double), typeof(BudgetBar),
            0.0, propertyChanged: (b, _, _) => ((BudgetBar)b).Refresh());

        public static readonly BindableProperty BarColorProperty = BindableProperty.Create(nameof(BarColor), typeof(Color), typeof(BudgetBar),
            Colors.Gray, propertyChanged: (b, _, _) => ((BudgetBar)b).Refresh());

        private readonly BoxView _fill = new();
        private readonly BoxView _notch = new() { WidthRequest = 2, Color = Colors.White, IsVisible = false };

        public BudgetBar()
        {
            var layout = new AbsoluteLayout { HeightRequest = 8 };
            layout.Add(_fill);
            layout.Add(_notch);
            AbsoluteLayout.SetLayoutFlags(_fill, Microsoft.Maui.Layouts.AbsoluteLayoutFlags.All);
            AbsoluteLayout.SetLayoutFlags(_notch, Microsoft.Maui.Layouts.AbsoluteLayoutFlags.HeightProportional
                | Microsoft.Maui.Layouts.AbsoluteLayoutFlags.XProportional);

            var track = new Border
            {
                StrokeThickness = 0,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 4 },
                HeightRequest = 8,
                Content = layout,
            };
            track.SetAppThemeColor(BackgroundColorProperty, Color.FromArgb("#E0E0E0"), Color.FromArgb("#404040"));
            Content = track;
            Refresh();
        }

        /// <summary>Spent as a percentage of the limit, not capped (216 = more than twice the limit).</summary>
        public double Used
        {
            get => (double)GetValue(UsedProperty);
            set => SetValue(UsedProperty, value);
        }

        public Color BarColor
        {
            get => (Color)GetValue(BarColorProperty);
            set => SetValue(BarColorProperty, value);
        }

        private void Refresh()
        {
            var share = Math.Clamp(Used / 100.0, 0, 1);
            AbsoluteLayout.SetLayoutBounds(_fill, new Rect(0, 0, share, 1));
            _fill.Color = BarColor;

            var over = Used > 100;
            _notch.IsVisible = over;
            if (over)
                AbsoluteLayout.SetLayoutBounds(_notch, new Rect(100.0 / Used, 0, 2, 1));
        }
    }
}
