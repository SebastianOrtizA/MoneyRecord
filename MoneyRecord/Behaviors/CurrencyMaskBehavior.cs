using MoneyRecord.Helpers;
using MoneyRecord.Services;
using System.Globalization;

namespace MoneyRecord.Behaviors
{
    /// <summary>
    /// A behavior that applies currency mask formatting to an Entry control.
    /// Formats input in <see cref="CurrencyCode"/> (empty = the base currency) with thousand separators, and limits
    /// the decimals to the currency's (0 for JPY/CLP/KRW, otherwise 2).
    /// Formatting is applied when the Entry loses focus to avoid Android EmojiCompat conflicts.
    /// Also formats when text is set programmatically (while Entry is not focused).
    /// </summary>
    public class CurrencyMaskBehavior : Behavior<Entry>
    {
        private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
        private Entry? _entry;
        private bool _isFocused;
        private bool _isFormatting;

        /// <summary>
        /// Gets or sets whether to show the currency symbol. Default is true.
        /// </summary>
        public bool ShowCurrencySymbol { get; set; } = true;

        public static readonly BindableProperty CurrencyCodeProperty =
            BindableProperty.Create(
                nameof(CurrencyCode),
                typeof(string),
                typeof(CurrencyMaskBehavior),
                null,
                propertyChanged: (bindable, oldValue, newValue) =>
                {
                    var behavior = (CurrencyMaskBehavior)bindable;
                    if (behavior._entry != null)
                        behavior.Reformat(behavior._entry, (string?)oldValue);
                });

        /// <summary>
        /// The currency of the amount (e.g. the selected account's). Empty = the base currency.
        /// </summary>
        public string? CurrencyCode
        {
            get => (string?)GetValue(CurrencyCodeProperty);
            set => SetValue(CurrencyCodeProperty, value);
        }

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

        private int DecimalPlaces => CurrencyService.GetMinorUnits(MoneyFormatter.ResolveCurrencyCode(CurrencyCode));

        protected override void OnAttachedTo(Entry entry)
        {
            base.OnAttachedTo(entry);
            _entry = entry;
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
            _entry = null;
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
                FormatIfNotFocused(entry);
        }

        /// <summary>The currency changed (another account was picked): show the amount in the new currency.</summary>
        private void Reformat(Entry entry, string? previousCurrencyCode)
        {
            if (_isFocused || string.IsNullOrEmpty(entry.Text))
                return;

            var value = ParseCurrencyValue(entry.Text, DecimalPlaces, AllowNegativeValues, previousCurrencyCode);
            SetText(entry, value != 0 ? FormatValue(value) : string.Empty);
        }

        private void FormatIfNotFocused(Entry entry)
        {
            if (_isFocused || _isFormatting)
                return;

            var text = entry.Text;
            if (string.IsNullOrEmpty(text))
                return;

            var value = ParseCurrencyValue(text, DecimalPlaces, AllowNegativeValues, CurrencyCode);
            if (value != 0 || text == "0")
            {
                var formatted = FormatValue(value);
                if (formatted != text)
                    SetText(entry, formatted);
            }
        }

        private void OnEntryFocused(object? sender, FocusEventArgs e)
        {
            _isFocused = true;

            if (sender is not Entry entry)
                return;

            // When focused, show raw numeric value for easier editing
            var value = ParseCurrencyValue(entry.Text, DecimalPlaces, AllowNegativeValues, CurrencyCode);
            SetText(entry, value != 0 ? value.ToString($"F{DecimalPlaces}", _culture) : string.Empty);
        }

        private void OnEntryUnfocused(object? sender, FocusEventArgs e)
        {
            _isFocused = false;

            if (sender is not Entry entry)
                return;

            // When unfocused, apply full currency formatting
            var value = ParseCurrencyValue(entry.Text, DecimalPlaces, AllowNegativeValues, CurrencyCode);
            SetText(entry, value != 0 ? FormatValue(value) : string.Empty);
        }

        private void SetText(Entry entry, string text)
        {
            _isFormatting = true;
            entry.Text = text;
            _isFormatting = false;
        }

        private string FormatValue(decimal value)
        {
            return ShowCurrencySymbol
                ? MoneyFormatter.Format(value, CurrencyCode, _culture)
                : value.ToString($"N{DecimalPlaces}", _culture);
        }

        /// <summary>
        /// Parses an amount typed in <paramref name="currencyCode"/> (empty = the base currency), rounded to the
        /// currency's decimals.
        /// </summary>
        public static decimal ParseAmount(string? text, string? currencyCode, bool allowNegative = false)
        {
            var decimals = CurrencyService.GetMinorUnits(MoneyFormatter.ResolveCurrencyCode(currencyCode));
            return ParseCurrencyValue(text, decimals, allowNegative, currencyCode);
        }

        /// <summary>
        /// Static helper method to extract the numeric value from a formatted currency string.
        /// Use this in ViewModels when parsing the bound Amount property; pass the amount's currency so its symbol
        /// (e.g. the "." in "S/.") is not mistaken for a decimal separator.
        /// </summary>
        public static decimal ParseCurrencyValue(string? text, int decimalPlaces = 2, bool allowNegative = false, string? currencyCode = null)
        {
            if (string.IsNullOrWhiteSpace(text))
                return 0m;

            var culture = CultureInfo.CurrentCulture;
            var decimalSep = culture.NumberFormat.CurrencyDecimalSeparator;
            var groupSep = culture.NumberFormat.CurrencyGroupSeparator;
            var negativeSign = culture.NumberFormat.NegativeSign;

            // Drop the currency symbol first: some contain separators ("S/.")
            var symbol = CurrencyService.GetSymbol(MoneyFormatter.ResolveCurrencyCode(currencyCode));
            text = text.Replace(symbol, string.Empty).Replace(culture.NumberFormat.CurrencySymbol, string.Empty);

            // Check for negative value indicators
            bool isNegative = allowNegative &&
                (text.Contains(negativeSign) || text.Contains('(') || text.Contains(')'));

            // Remove group separators, keep only digits and decimal separator
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
