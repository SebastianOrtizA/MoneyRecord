using WinAccelerator = Microsoft.UI.Xaml.Input.KeyboardAccelerator;
using Windows.System;

namespace MoneyRecord.Platforms.Windows
{
    /// <summary>Desktop behaviour of the main window: a size the layouts still fit in, and the keyboard back.</summary>
    internal static class WindowsDesktop
    {
        private const double MinWidth = 420;
        private const double MinHeight = 600;

        public static void Configure(Window window)
        {
            window.MinimumWidth = MinWidth;
            window.MinimumHeight = MinHeight;

            if (window.Handler?.PlatformView is not Microsoft.UI.Xaml.Window native || native.Content is null)
                return;

            // Alt+Left and Esc do what the Android back gesture does: close a sheet, then the previous page
            native.Content.KeyboardAccelerators.Add(Back(VirtualKey.Left, VirtualKeyModifiers.Menu));
            native.Content.KeyboardAccelerators.Add(Back(VirtualKey.Escape, VirtualKeyModifiers.None));
        }

        private static WinAccelerator Back(VirtualKey key, VirtualKeyModifiers modifiers)
        {
            var accelerator = new WinAccelerator { Key = key, Modifiers = modifiers };
            accelerator.Invoked += (s, e) =>
            {
                if (Shell.Current is { } shell)
                    e.Handled = shell.SendBackButtonPressed();
            };
            return accelerator;
        }
    }
}
