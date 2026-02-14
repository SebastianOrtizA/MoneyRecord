using Microsoft.Maui.Handlers;
using MoneyRecord.Controls;
using UIKit;

namespace MoneyRecord.Platforms.iOS.Handlers
{
    /// <summary>
    /// iOS-specific handler for DecimalEntry that configures the keyboard
    /// to show decimal pad based on device locale.
    /// </summary>
    public static class DecimalEntryHandler
    {
        public static void Configure()
        {
            EntryHandler.Mapper.AppendToMapping(nameof(DecimalEntry), (handler, view) =>
            {
                if (view is DecimalEntry && handler.PlatformView is UITextField textField)
                {
                    // Set keyboard type to DecimalPad
                    // This shows numeric keyboard with decimal separator based on device locale
                    textField.KeyboardType = UIKeyboardType.DecimalPad;
                }
            });
        }
    }
}
