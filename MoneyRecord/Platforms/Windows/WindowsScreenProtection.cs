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
        private const uint WdaExcludeFromCapture = 0x11;

        [DllImport("user32.dll")]
        private static extern bool SetWindowDisplayAffinity(IntPtr hwnd, uint affinity);

        public bool IsSupported => true;

        public void SetSecure(bool secure)
        {
            void Apply()
            {
                if (Microsoft.Maui.Controls.Application.Current?.Windows is not { Count: > 0 } windows
                    || windows[0].Handler?.PlatformView is not MauiWinUIWindow window)
                    return;
                SetWindowDisplayAffinity(WindowNative.GetWindowHandle(window), secure ? WdaExcludeFromCapture : WdaNone);
            }

            if (MainThread.IsMainThread)
                Apply();
            else
                MainThread.BeginInvokeOnMainThread(Apply);
        }
    }
}
