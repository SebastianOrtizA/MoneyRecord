using System.Globalization;

namespace MoneyRecord.Behaviors
{
    /// <summary>
    /// A behavior that applies currency mask formatting to an Entry control.
    /// Formats input as currency with proper thousand separators and limits to 2 decimal places.
    /// Formatting is applied when the Entry loses focus to avoid Android EmojiCompat conflicts.
    /// Also formats when text is set programmatically (while Entry is not focused).
    /// </summary>
    public class CurrencyMaskBehavior : Behavior<Entry>
    {
        private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
        private bool _isFocused;
        private bool _isFormatting;

        /// <summary>
        /// Gets or sets whether to show the currency symbol. Default is true.
        /// </summary>
        public bool ShowCurrencySymbol { get; set; } = true;

        /// <summary>
        /// Gets or sets the maximum number of decimal places. Default is 2.
        /// </summary>
        public int DecimalPlaces { get; set; } = 2;

        /// <summary>
        /// Bindable property for AllowNegativeValues.
        /// </summary>
        public static readonly BindableProperty AllowNegativeValuesProperty =
            BindableProperty.Create(
                nameof(AllowNegativeValues),
                typeof(bool),
                typeof(CurrencyMaskBehavior),
                false);

        /// <summary>
        /// Gets or sets whether negative values are allowed. Default is false.
        /// </summary>
        public bool AllowNegativeValues
        {
            get => (bool)GetValue(AllowNegativeValuesProperty);
            set => SetValue(AllowNegativeValuesProperty, value);
        }

        protected override void OnAttachedTo(Entry entry)
        {
            base.OnAttachedTo(entry);
            entry.Unfocused += OnEntryUnfocused;
            entry.Focused += OnEntryFocused;
            entry.TextChanged += OnEntryTextChanged;
            entry.BindingContextChanged += OnEntryBindingContextChanged;

            // Inherit binding context from parent
            BindingContext = entry.BindingContext;

            // Format initial value if present
            FormatIfNotFocused(entry);
        }

        protected override void OnDetachingFrom(Entry entry)
        {
            entry.Unfocused -= OnEntryUnfocused;
            entry.Focused -= OnEntryFocused;
            entry.TextChanged -= OnEntryTextChanged;
            entry.BindingContextChanged -= OnEntryBindingContextChanged;
            base.OnDetachingFrom(entry);
        }

        private void OnEntryBindingContextChanged(object? sender, EventArgs e)
        {
            if (sender is Entry entry)
            {
                BindingContext = entry.BindingContext;
            }
        }

        private void OnEntryTextChanged(object? sender, TextChangedEventArgs e)
        {
            if (sender is not Entry entry || _isFormatting)
                return;

            // Only format if the Entry is not focused (programmatic change from ViewModel)
            if (!_isFocused && !string.IsNullOrEmpty(e.NewTextValue))
            {
                // Check if already formatted (contains currency symbol)
                if (!e.NewTextValue.Contains(_culture.NumberFormat.CurrencySymbol))
                {
                    FormatIfNotFocused(entry);
                }
            }
        }

        private void FormatIfNotFocused(Entry entry)
        {
            if (_isFocused || _isFormatting)
                return;

            var text = entry.Text;
            if (string.IsNullOrEmpty(text))
                return;

            var value = ParseCurrencyValue(text, DecimalPlaces, AllowNegativeValues);
            if (value != 0 || text == "0")
            {
                _isFormatting = true;
                entry.Text = FormatValue(value);
                _isFormatting = false;
            }
        }

        private void OnEntryFocused(object? sender, FocusEventArgs e)
        {
            _isFocused = true;

            if (sender is not Entry entry)
                return;

            // When focused, show raw numeric value for easier editing
            var value = ParseCurrencyValue(entry.Text, DecimalPlaces, AllowNegativeValues);
            if (value != 0)
            {
                // Show plain number without currency symbol for editing
                _isFormatting = true;
                entry.Text = value.ToString($"F{DecimalPlaces}", _culture);
                _isFormatting = false;
            }
            else
            {
                entry.Text = string.Empty;
            }
        }

        private void OnEntryUnfocused(object? sender, FocusEventArgs e)
        {
            _isFocused = false;

            if (sender is not Entry entry)
                return;

            // When unfocused, apply full currency formatting
            var value = ParseCurrencyValue(entry.Text, DecimalPlaces, AllowNegativeValues);

            _isFormatting = true;
            if (value != 0)
            {
                entry.Text = FormatValue(value);
            }
            else
            {
                entry.Text = string.Empty;
            }
            _isFormatting = false;
        }

        private string FormatValue(decimal value)
        {
            if (ShowCurrencySymbol)
            {
                return value.ToString("C", _culture);
            }
            return value.ToString($"N{DecimalPlaces}", _culture);
        }

        /// <summary>
        /// Static helper method to extract the numeric value from a formatted currency string.
        /// Use this in ViewModels when parsing the bound Amount property.
        /// </summary>
        public static decimal ParseCurrencyValue(string? text, int decimalPlaces = 2, bool allowNegative = false)
        {
            if (string.IsNullOrWhiteSpace(text))
                return 0m;

            var culture = CultureInfo.CurrentCulture;
            var decimalSep = culture.NumberFormat.CurrencyDecimalSeparator;
            var groupSep = culture.NumberFormat.CurrencyGroupSeparator;
            var negativeSign = culture.NumberFormat.NegativeSign;

            // Check for negative value indicators
            bool isNegative = allowNegative && 
                (text.Contains(negativeSign) || text.Contains('(') || text.Contains(')'));

            // Remove currency symbol and group separators, keep only digits and decimal separator
            var cleaned = new System.Text.StringBuilder();
            var hasDecimal = false;
            var decimalDigits = 0;

            foreach (var c in text)
            {
                // Skip group separators (thousand separators)
                if (c.ToString() == groupSep)
                    continue;

                // Skip negative sign and parentheses (handled separately)
                if (c.ToString() == negativeSign || c == '(' || c == ')')
                    continue;

                if (char.IsDigit(c))
                {
                    if (hasDecimal)
                    {
                        // Limit decimal places
                        if (decimalDigits < decimalPlaces)
                        {
                            cleaned.Append(c);
                            decimalDigits++;
                        }
                    }
                    else
                    {
                        cleaned.Append(c);
                    }
                }
                else if (c.ToString() == decimalSep && !hasDecimal)
                {
                    // Only the culture's decimal separator marks the decimal point
                    cleaned.Append(decimalSep);
                    hasDecimal = true;
                }
                else if ((c == '.' || c == ',') && !hasDecimal && c.ToString() != groupSep)
                {
                    // Handle manual input with . or , as decimal (when different from group separator)
                    cleaned.Append(decimalSep);
                    hasDecimal = true;
                }
            }

            if (decimal.TryParse(cleaned.ToString(), NumberStyles.Number, culture, out var result))
            {
                var roundedResult = Math.Round(result, decimalPlaces);
                return isNegative ? -roundedResult : roundedResult;
            }

            return 0m;
        }
    }
}
