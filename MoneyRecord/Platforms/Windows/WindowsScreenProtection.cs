using System.Runtime.InteropServices;
using Microsoft.Maui.Platform;
using MoneyRecord.Services.Interfaces;
using WinRT.Interop;

namespace MoneyRecord.Platforms.Windows
{
    /// <summary>
    /// SetWindowDisplayAffinity: the window is left out of screenshots and screen recordings (it shows black). Needs
    /// Windows 10 2004 or later for the exclude-from-capture mode; older builds only black it out for capture tools.
    /// </summary>
    public sealed class WindowsScreenProtection : IScreenProtection
    {
        private const uint WdaNone = 0;
        private const uint WdaMonitor = 1;
        private const uint WdaExcludeFromCapture = 0x11;

        [DllImport("user32.dll")]
        private static extern bool SetWindowDisplayAffinity(IntPtr hwnd, uint affinity);

        public bool IsSupported => true;

        public void SetSecure(bool secure)
        {
            // One window today: only the first is protected
            void Apply()
            {
                if (Microsoft.Maui.Controls.Application.Current?.Windows is not { Count: > 0 } windows
                    || windows[0].Handler?.PlatformView is not MauiWinUIWindow window)
                    return;
                var hwnd = WindowNative.GetWindowHandle(window);
                if (!secure)
                    SetWindowDisplayAffinity(hwnd, WdaNone);
                // Before Windows 10 2004 the exclude mode is refused: black the window out for capture tools instead
                else if (!SetWindowDisplayAffinity(hwnd, WdaExcludeFromCapture))
                    SetWindowDisplayAffinity(hwnd, WdaMonitor);
            }

            if (MainThread.IsMainThread)
                Apply();
            else
                MainThread.BeginInvokeOnMainThread(Apply);
        }
    }
}
