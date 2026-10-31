namespace MoneyRecord.Controls
{
    /// <summary>
    /// A one-line label that shrinks its font (from <see cref="MaxFontSize"/> down to <see cref="MinFontSize"/>) until the
    /// text fits its width: a long amount ("$ 7.771.515,00" in a narrow card) stays whole instead of wrapping. Give it
    /// the width to use (<c>HorizontalOptions="Fill"</c>) and centre the text with <c>HorizontalTextAlignment</c>.
    /// With <c>MaxLines</c> above 1 (and a wrapping <c>LineBreakMode</c>) the text wraps between words and only its longest
    /// word has to fit: a name like "Entretenimiento" shrinks instead of being split mid-word.
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
        private double? _wrappedHeight;

        public AutoFitLabel()
        {
            FontFamily = "OpenSansRegular"; // the Label style does not reach a subclass, so amounts fell back to Roboto
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
            if (propertyName is nameof(Text) or nameof(FontAttributes) or nameof(FontFamily) or nameof(MaxLines))
                Refit();
        }

        // A fit before the platform view existed measured without it (and skipped the wrapped height)
        protected override void OnHandlerChanged()
        {
            base.OnHandlerChanged();
            Refit();
        }

        private void Refit()
        {
            _fitted = default;
            _wrappedHeight = null;
            Fit(Width);
        }

        /// <summary>
        /// The width at <paramref name="fontSize"/> of the text's one line, or of its widest word when it may wrap
        /// (<c>MaxLines</c> above 1). Android measures with the view's own paint (its font and weight, the system font
        /// scale): the layout measure can still hold the previous text right after a change. Elsewhere only a one-line
        /// text is measured; a wrapping one keeps its size.
        /// </summary>
        private double TextWidth(double fontSize)
        {
#if ANDROID
            if (Handler?.PlatformView is Android.Widget.TextView view && view.Context?.Resources?.DisplayMetrics is { } metrics)
            {
                using var paint = new Android.Text.TextPaint(view.Paint);
                paint.TextSize = Android.Util.TypedValue.ApplyDimension(Android.Util.ComplexUnitType.Sp, (float)fontSize, metrics);
                var text = Text ?? string.Empty;
                var parts = MaxLines > 1 ? text.Split(' ', StringSplitOptions.RemoveEmptyEntries) : [text];
                return parts.Length == 0 ? 0 : parts.Max(part => paint.MeasureText(part)) / metrics.Density;
            }
#endif
            return MaxLines > 1 ? 0 : Measure(double.PositiveInfinity, double.PositiveInfinity).Width;
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

        /// <summary>
        /// A wrapping label is as tall as <c>MaxLines</c> lines at <see cref="MaxFontSize"/>, whatever its text: a
        /// <c>FlexLayout</c> measures its children at an unlimited width (one line) and keeps that height, so a second line
        /// would be cut. Measured with the view's own paint, so it follows the system font size.
        /// </summary>
        protected override Size MeasureOverride(double widthConstraint, double heightConstraint)
        {
            var size = base.MeasureOverride(widthConstraint, heightConstraint);
            return WrappedHeight() is { } height ? new Size(size.Width, height) : size;
        }

        /// <summary>
        /// <c>MaxLines</c> lines at <see cref="MaxFontSize"/>, or null for a one-line label (or before the platform view
        /// exists). Kept until <see cref="Refit"/>: it depends only on those two and the font scale.
        /// </summary>
        private double? WrappedHeight()
        {
            if (_wrappedHeight != null || MaxLines <= 1)
                return _wrappedHeight;
#if ANDROID
            if (OperatingSystem.IsAndroidVersionAtLeast(23)
                && Handler?.PlatformView is Android.Widget.TextView view
                && view.Context?.Resources?.DisplayMetrics is { } metrics)
            {
                using var paint = new Android.Text.TextPaint(view.Paint);
                paint.TextSize = Android.Util.TypedValue.ApplyDimension(Android.Util.ComplexUnitType.Sp, (float)MaxFontSize, metrics);
                var lines = string.Join('\n', Enumerable.Repeat("Ag", MaxLines));
                using var layout = Android.Text.StaticLayout.Builder.Obtain(lines, 0, lines.Length, paint, int.MaxValue / 2).Build();
                _wrappedHeight = Math.Ceiling(layout.Height / metrics.Density);
            }
#endif
            return _wrappedHeight;
        }
    }
}
