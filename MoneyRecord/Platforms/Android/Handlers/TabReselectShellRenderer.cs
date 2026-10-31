using Android.Views;
using Android.Widget;
using Google.Android.Material.BottomNavigation;
using Google.Android.Material.Navigation;
using Google.Android.Material.Tabs;
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

        protected override IShellTabLayoutAppearanceTracker CreateTabLayoutAppearanceTracker(ShellSection shellSection) =>
            new SentenceCaseTabs(base.CreateTabLayoutAppearanceTracker(shellSection));

        /// <summary>
        /// The Reports top tabs: the default upper case with letter spacing made the fifth title run off the screen,
        /// so titles are sentence case at 14 sp and four and a half tabs show.
        /// </summary>
        private sealed class SentenceCaseTabs(IShellTabLayoutAppearanceTracker inner) : IShellTabLayoutAppearanceTracker
        {
            public void SetAppearance(TabLayout tabLayout, ShellAppearance appearance)
            {
                inner.SetAppearance(tabLayout, appearance);

                // Unselected titles had too little contrast (secondary text colour, like the rest of the app)
                var dark = Application.Current?.RequestedTheme == AppTheme.Dark;
                tabLayout.SetTabTextColors(
                    global::Android.Graphics.Color.ParseColor(dark ? "#AAAAAA" : "#666666"),
                    dark ? global::Android.Graphics.Color.White : global::Android.Graphics.Color.Black);

                if (tabLayout.GetChildAt(0) is not ViewGroup strip)
                    return;

                for (var i = 0; i < strip.ChildCount; i++)
                {
                    if (strip.GetChildAt(i) is not ViewGroup tab)
                        continue;
                    for (var j = 0; j < tab.ChildCount; j++)
                    {
                        if (tab.GetChildAt(j) is TextView label)
                        {
                            label.SetAllCaps(false);
                            label.LetterSpacing = 0;
                            label.SetTextSize(global::Android.Util.ComplexUnitType.Sp, 14);
                        }
                    }
                }
            }

            public void ResetAppearance(TabLayout tabLayout) => inner.ResetAppearance(tabLayout);

            public void Dispose() => inner.Dispose();
        }

        /// <summary>
        /// Keeps the default look and hooks the bottom bar it is handed (MAUI gives the tracker the bar on every
        /// appearance change, the first one included).
        /// </summary>
        private sealed class ReselectTracker(IShellBottomNavViewAppearanceTracker inner, ShellItem shellItem)
            : IShellBottomNavViewAppearanceTracker
        {
            private readonly ReselectListener _listener = new(shellItem);

            public void SetAppearance(BottomNavigationView bottomView, IShellAppearanceElement appearance)
            {
                inner.SetAppearance(bottomView, appearance);
                bottomView.SetOnItemReselectedListener(_listener);
            }

            public void ResetAppearance(BottomNavigationView bottomView)
            {
                inner.ResetAppearance(bottomView);
                bottomView.SetOnItemReselectedListener(_listener);
            }

            // The listener stays with the bottom bar, which may still call it: left to the GC with the bar
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
