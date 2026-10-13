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
        // Read on each use: the language (and with it the separators) can change while the page is open
        private static CultureInfo Culture => CultureInfo.CurrentCulture;
        private Entry? _entry;
        private bool _isFocused;
        private bool _isFormatting;

        /// <summary>The amount to the cent; the text may show fewer decimals (0 for JPY/CLP/KRW).</summary>
        private decimal? _exactValue;

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

        /// <summary>
        /// The currency changed (another account was picked): show the amount in the new currency. Uses the exact
        /// value, not the shown text, so passing through a zero-decimal currency (USD → JPY → USD) keeps the cents.
        /// </summary>
        private void Reformat(Entry entry, string? previousCurrencyCode)
        {
            if (_isFocused || string.IsNullOrEmpty(entry.Text))
                return;

            var value = _exactValue ?? AmountParser.ParseCurrencyValue(entry.Text, Money.Decimals, AllowNegativeValues, previousCurrencyCode);
            SetText(entry, value != 0 ? FormatValue(value) : string.Empty);
        }

        /// <summary>
        /// A value set by the ViewModel (e.g. a loaded amount). It is read to the cent, whatever the current currency,
        /// because the account (and with it the currency) may only be selected after the amount.
        /// </summary>
        private void FormatIfNotFocused(Entry entry)
        {
            if (_isFocused || _isFormatting)
                return;

            var text = entry.Text;
            if (string.IsNullOrEmpty(text))
            {
                _exactValue = null;
                return;
            }

            var value = AmountParser.ParseCurrencyValue(text, Money.Decimals, AllowNegativeValues, CurrencyCode);
            if (value != 0 || text == "0")
            {
                _exactValue = value;
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
            var value = AmountParser.ParseCurrencyValue(entry.Text, DecimalPlaces, AllowNegativeValues, CurrencyCode);
            SetText(entry, value != 0 ? value.ToString($"F{DecimalPlaces}", Culture) : string.Empty);
        }

        private void OnEntryUnfocused(object? sender, FocusEventArgs e)
        {
            _isFocused = false;

            if (sender is not Entry entry)
                return;

            // When unfocused, apply full currency formatting; what the user typed is the exact value
            var value = AmountParser.ParseCurrencyValue(entry.Text, DecimalPlaces, AllowNegativeValues, CurrencyCode);
            _exactValue = value != 0 ? value : null;
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
                ? MoneyFormatter.Format(value, CurrencyCode, Culture)
                : value.ToString($"N{DecimalPlaces}", Culture);
        }
    }
}
