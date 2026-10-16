namespace MoneyRecord.Controls
{
    /// <summary>
    /// A one-line label that shrinks its font (from <see cref="MaxFontSize"/> down to <see cref="MinFontSize"/>) until the
    /// text fits its width: a long amount ("$ 7.771.515,00" in a narrow card) stays whole instead of wrapping. Give it
    /// the width to use (<c>HorizontalOptions="Fill"</c>) and centre the text with <c>HorizontalTextAlignment</c>.
    /// </summary>
    public class AutoFitLabel : Label
    {
        public static readonly BindableProperty MaxFontSizeProperty =
            BindableProperty.Create(nameof(MaxFontSize), typeof(double), typeof(AutoFitLabel), 16d,
                propertyChanged: (b, _, _) => ((AutoFitLabel)b).Refit());

        public static readonly BindableProperty MinFontSizeProperty =
            BindableProperty.Create(nameof(MinFontSize), typeof(double), typeof(AutoFitLabel), 10d,
                propertyChanged: (b, _, _) => ((AutoFitLabel)b).Refit());

        private const double Step = 0.5;

        // The width and text the font was last fitted to: a resize from the font change itself doesn't refit
        private (double Width, string? Text) _fitted;
        private bool _fitting;

        public AutoFitLabel()
        {
            LineBreakMode = LineBreakMode.NoWrap;
            MaxLines = 1;
            FontSize = MaxFontSize;
        }

        /// <summary>The font size when the text fits (the usual look).</summary>
        public double MaxFontSize
        {
            get => (double)GetValue(MaxFontSizeProperty);
            set => SetValue(MaxFontSizeProperty, value);
        }

        /// <summary>The smallest font it shrinks to; a text that still doesn't fit is cut at the end.</summary>
        public double MinFontSize
        {
            get => (double)GetValue(MinFontSizeProperty);
            set => SetValue(MinFontSizeProperty, value);
        }

        protected override void OnSizeAllocated(double width, double height)
        {
            base.OnSizeAllocated(width, height);
            Fit(width);
        }

        protected override void OnPropertyChanged(string? propertyName = null)
        {
            base.OnPropertyChanged(propertyName);
            if (propertyName is nameof(Text) or nameof(FontAttributes) or nameof(FontFamily))
                Refit();
        }

        private void Refit()
        {
            _fitted = default;
            Fit(Width);
        }

        /// <summary>
        /// The text's one-line width at <paramref name="fontSize"/>. Android measures with the view's own paint (its font and
        /// weight, the system font scale): the layout measure can still hold the previous text right after a change.
        /// </summary>
        private double TextWidth(double fontSize)
        {
#if ANDROID
            if (Handler?.PlatformView is Android.Widget.TextView view && view.Context?.Resources?.DisplayMetrics is { } metrics)
            {
                using var paint = new Android.Text.TextPaint(view.Paint);
                paint.TextSize = Android.Util.TypedValue.ApplyDimension(Android.Util.ComplexUnitType.Sp, (float)fontSize, metrics);
                return paint.MeasureText(Text ?? string.Empty) / metrics.Density;
            }
#endif
            return Measure(double.PositiveInfinity, double.PositiveInfinity).Width;
        }

        private void Fit(double width)
        {
            if (_fitting || width <= 0 || _fitted == (width, Text))
                return;

            _fitting = true;
            try
            {
                // Largest size whose one-line width fits, measured unconstrained
                var size = MaxFontSize;
                FontSize = size;
                while (size - Step >= MinFontSize && TextWidth(size) > width)
                {
                    size -= Step;
                    FontSize = size;
                }

                _fitted = (width, Text);
            }
            finally
            {
                _fitting = false;
            }
        }
    }
}
