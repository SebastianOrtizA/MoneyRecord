using Android.Views;
using Google.Android.Material.BottomNavigation;
using Google.Android.Material.Navigation;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls.Handlers.Compatibility;
using Microsoft.Maui.Controls.Platform.Compatibility;

namespace MoneyRecord.Platforms.Android.Handlers
{
    /// <summary>
    /// The Shell with one addition: tapping the tab already shown goes back to its own page (Android's bottom bar only
    /// reports a tab change, so a page pushed in the current tab stayed open).
    /// </summary>
    public class TabReselectShellRenderer : ShellRenderer
    {
        protected override IShellBottomNavViewAppearanceTracker CreateBottomNavViewAppearanceTracker(ShellItem shellItem) =>
            new ReselectTracker(base.CreateBottomNavViewAppearanceTracker(shellItem), shellItem);

        /// <summary>
        /// Keeps the default look and hooks the bottom bar it is handed (MAUI gives the tracker the bar on every
        /// appearance change, the first one included).
        /// </summary>
        private sealed class ReselectTracker(IShellBottomNavViewAppearanceTracker inner, ShellItem shellItem)
            : IShellBottomNavViewAppearanceTracker
        {
            public void SetAppearance(BottomNavigationView bottomView, IShellAppearanceElement appearance)
            {
                inner.SetAppearance(bottomView, appearance);
                bottomView.SetOnItemReselectedListener(new ReselectListener(shellItem));
            }

            public void ResetAppearance(BottomNavigationView bottomView)
            {
                inner.ResetAppearance(bottomView);
                bottomView.SetOnItemReselectedListener(new ReselectListener(shellItem));
            }

            public void Dispose() => inner.Dispose();
        }

        private sealed class ReselectListener(ShellItem shellItem)
            : Java.Lang.Object, NavigationBarView.IOnItemReselectedListener
        {
            public async void OnNavigationItemReselected(IMenuItem item)
            {
                if (shellItem.CurrentItem is not { } section || section.Navigation.NavigationStack.Count <= 1)
                    return;

                // async void: an exception here would crash the app
                try
                {
                    await section.Navigation.PopToRootAsync();
                }
                catch (Exception ex)
                {
                    IPlatformApplication.Current?.Services.GetService<ILogger<TabReselectShellRenderer>>()
                        ?.LogError(ex, "Going back to the tab's page failed");
                }
            }
        }
    }
}
