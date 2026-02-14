namespace MoneyRecord.Controls
{
    /// <summary>
    /// A custom Entry control that displays a decimal numeric keyboard on mobile devices.
    /// The decimal separator automatically matches the device's regional settings.
    /// </summary>
    public class DecimalEntry : Entry
    {
        public DecimalEntry()
        {
            // Set default keyboard to Numeric as fallback
            Keyboard = Keyboard.Numeric;
        }
    }
}
