using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using WinAccelerator = Microsoft.UI.Xaml.Input.KeyboardAccelerator;

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
            native.Content.KeyboardAccelerators.Add(Back(window, native, VirtualKey.Left, VirtualKeyModifiers.Menu, isEsc: false));
            native.Content.KeyboardAccelerators.Add(Back(window, native, VirtualKey.Escape, VirtualKeyModifiers.None, isEsc: true));
        }

        private static WinAccelerator Back(Window window, Microsoft.UI.Xaml.Window native, VirtualKey key, VirtualKeyModifiers modifiers, bool isEsc)
        {
            var accelerator = new WinAccelerator { Key = key, Modifiers = modifiers };
            accelerator.Invoked += (s, e) =>
            {
                // Esc belongs to a field being typed in: it must not leave the page and drop the entry
                if (isEsc && native.Content.XamlRoot is { } root
                    && FocusManager.GetFocusedElement(root) is TextBox or PasswordBox or AutoSuggestBox)
                    return;

                // Same route as MainActivity: a modal (PIN, password, pickers) gets back before the Shell, and the first-run
                // setup (no Shell yet) is the window's page
                var page = window.Page;
                if (page is null)
                    return;
                var target = page.Navigation.ModalStack.LastOrDefault() ?? page;
                e.Handled = target.SendBackButtonPressed();
            };
            return accelerator;
        }
    }
}
