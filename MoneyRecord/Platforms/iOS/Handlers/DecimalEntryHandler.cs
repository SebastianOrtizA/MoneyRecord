using Microsoft.Maui.Handlers;
using MoneyRecord.Controls;
using UIKit;
using Foundation;

namespace MoneyRecord.Platforms.iOS.Handlers
{
    /// <summary>
    /// iOS-specific handler for DecimalEntry that configures the keyboard
    /// to show decimal pad based on device locale.
    /// Supports signed input (negative values) when AllowNegative is true
    /// by adding a minus button to the keyboard accessory toolbar.
    /// </summary>
    public static class DecimalEntryHandler
    {
        public static void Configure()
        {
            EntryHandler.Mapper.AppendToMapping(nameof(DecimalEntry), (handler, view) =>
            {
                if (view is DecimalEntry decimalEntry && handler.PlatformView is UITextField textField)
                {
                    ConfigureKeyboard(textField, decimalEntry.AllowNegative);
                }
            });

            // Also map the AllowNegative property changes
            EntryHandler.Mapper.AppendToMapping(nameof(DecimalEntry.AllowNegative), (handler, view) =>
            {
                if (view is DecimalEntry decimalEntry && handler.PlatformView is UITextField textField)
                {
                    ConfigureKeyboard(textField, decimalEntry.AllowNegative);
                }
            });
        }

        private static void ConfigureKeyboard(UITextField textField, bool allowNegative)
        {
            // Set keyboard type to DecimalPad
            // This shows numeric keyboard with decimal separator based on device locale
            textField.KeyboardType = UIKeyboardType.DecimalPad;

            if (allowNegative)
            {
                // Add a toolbar with a minus button for negative input
                var toolbar = new UIToolbar(new CoreGraphics.CGRect(0, 0, UIScreen.MainScreen.Bounds.Width, 44));

                var minusButton = new UIBarButtonItem("−", UIBarButtonItemStyle.Plain, (sender, e) =>
                {
                    var currentText = textField.Text ?? string.Empty;

                    if (currentText.StartsWith("-"))
                    {
                        // Remove the minus sign
                        textField.Text = currentText.Substring(1);
                    }
                    else
                    {
                        // Add the minus sign at the beginning
                        textField.Text = "-" + currentText;
                    }

                    // Notify that the text changed
                    textField.SendActionForControlEvents(UIControlEvent.EditingChanged);
                });

                var flexSpace = new UIBarButtonItem(UIBarButtonSystemItem.FlexibleSpace);
                var doneButton = new UIBarButtonItem(UIBarButtonSystemItem.Done, (sender, e) =>
                {
                    textField.ResignFirstResponder();
                });

                toolbar.Items = [minusButton, flexSpace, doneButton];
                textField.InputAccessoryView = toolbar;
            }
            else
            {
                // Remove toolbar if not allowing negative values
                textField.InputAccessoryView = null;
            }
        }
    }
}
