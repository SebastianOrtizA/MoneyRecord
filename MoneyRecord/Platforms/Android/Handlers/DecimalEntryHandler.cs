using Android.Text;
using Android.Text.Method;
using Microsoft.Maui.Handlers;
using MoneyRecord.Controls;
using System.Globalization;

namespace MoneyRecord.Platforms.Android.Handlers
{
    /// <summary>
    /// Android-specific handler for DecimalEntry that configures the keyboard
    /// to show numeric input with decimal support based on device locale.
    /// </summary>
    public static class DecimalEntryHandler
    {
        public static void Configure()
        {
            EntryHandler.Mapper.AppendToMapping(nameof(DecimalEntry), (handler, view) =>
            {
                if (view is DecimalEntry && handler.PlatformView is AndroidX.AppCompat.Widget.AppCompatEditText editText)
                {
                    // Use SetRawInputType to show decimal keyboard without character filtering
                    editText.SetRawInputType(InputTypes.ClassNumber | InputTypes.NumberFlagDecimal);

                    // Get the locale's decimal separator and allow both comma and period
                    var decimalSeparator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;

                    // Create a DigitsKeyListener that accepts digits plus both decimal separators
                    // This ensures users can input decimals regardless of keyboard layout
                    editText.KeyListener = DigitsKeyListener.GetInstance("0123456789.,");
                }
            });
        }
    }
}
