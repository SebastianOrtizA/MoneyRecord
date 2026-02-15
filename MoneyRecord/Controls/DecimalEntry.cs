namespace MoneyRecord.Controls
{
    /// <summary>
    /// A custom Entry control that displays a decimal numeric keyboard on mobile devices.
    /// The decimal separator automatically matches the device's regional settings.
    /// Supports optional signed input (negative values) via the AllowNegative property.
    /// </summary>
    public class DecimalEntry : Entry
    {
        /// <summary>
        /// Bindable property for AllowNegative.
        /// </summary>
        public static readonly BindableProperty AllowNegativeProperty =
            BindableProperty.Create(
                nameof(AllowNegative),
                typeof(bool),
                typeof(DecimalEntry),
                false,
                propertyChanged: OnAllowNegativeChanged);

        /// <summary>
        /// Gets or sets whether negative values are allowed. 
        /// When true, uses a signed numeric keyboard that includes the minus sign.
        /// Default is false.
        /// </summary>
        public bool AllowNegative
        {
            get => (bool)GetValue(AllowNegativeProperty);
            set => SetValue(AllowNegativeProperty, value);
        }

        public DecimalEntry()
        {
            // Set default keyboard to Numeric
            Keyboard = Keyboard.Numeric;
        }

        private static void OnAllowNegativeChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is DecimalEntry entry)
            {
                // Force handler update by toggling a property that triggers re-mapping
                entry.Handler?.UpdateValue(nameof(AllowNegative));
            }
        }
    }
}
