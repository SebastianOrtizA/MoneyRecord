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
    /// Supports signed input (negative values) when AllowNegative is true.
    /// </summary>
    public static class DecimalEntryHandler
    {
        public static void Configure()
        {
            EntryHandler.Mapper.AppendToMapping(nameof(DecimalEntry), (handler, view) =>
            {
                if (view is DecimalEntry decimalEntry && handler.PlatformView is AndroidX.AppCompat.Widget.AppCompatEditText editText)
                {
                    ConfigureInputType(editText, decimalEntry.AllowNegative);
                }
            });

            // Also map the AllowNegative property changes
            EntryHandler.Mapper.AppendToMapping(nameof(DecimalEntry.AllowNegative), (handler, view) =>
            {
                if (view is DecimalEntry decimalEntry && handler.PlatformView is AndroidX.AppCompat.Widget.AppCompatEditText editText)
                {
                    ConfigureInputType(editText, decimalEntry.AllowNegative);
                }
            });
        }

        private static void ConfigureInputType(AndroidX.AppCompat.Widget.AppCompatEditText editText, bool allowNegative)
        {
            // Build input type flags
            var inputType = InputTypes.ClassNumber | InputTypes.NumberFlagDecimal;

            if (allowNegative)
            {
                inputType |= InputTypes.NumberFlagSigned;
            }

            // Use SetRawInputType to show decimal keyboard without character filtering
            editText.SetRawInputType(inputType);

            // Create a DigitsKeyListener that accepts digits plus decimal separators
            // and optionally the minus sign for negative values
            var allowedChars = allowNegative ? "0123456789.,-" : "0123456789.,";
            editText.KeyListener = DigitsKeyListener.GetInstance(allowedChars);
        }
    }
}
