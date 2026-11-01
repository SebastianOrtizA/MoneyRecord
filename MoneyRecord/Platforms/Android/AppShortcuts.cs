using Android.Content;
using AndroidX.Core.Content.PM;
using AndroidX.Core.Graphics.Drawable;
using Microsoft.Extensions.Logging;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Platforms.Android
{
    /// <summary>
    /// The launcher icon's long-press menu: Expense / Income / Transfer, each opening its add form (the route goes through
    /// <see cref="MainActivity.RouteExtra"/> like a widget button, so the app lock still comes first). Published as dynamic
    /// shortcuts, not a static shortcuts.xml: the labels follow the app's language, and no activity class name is needed.
    /// </summary>
    public static class AppShortcuts
    {
        public static async Task PublishAsync(Context context)
        {
            try
            {
                var services = IPlatformApplication.Current?.Services;
                var quick = new List<ShortcutInfoCompat>();
                // The launcher shows only a few (often 4): the three add forms stay, quick entries take what is left. Not
                // while Hide balances is on - the label is only a name, but the menu is visible without unlocking the app
                var room = ShortcutManagerCompat.GetMaxShortcutCountPerActivity(context) - 3;
                if (room > 0 && services?.GetService<IPrivacySettings>() is { HideBalances: false } && services.GetService<IQuickEntryService>() is { } entries)
                {
                    foreach (var item in QuickEntryShortcuts.Top(await entries.GetAllAsync(), Math.Min(room, QuickEntryShortcuts.MaxCount)))
                        quick.Add(Create(context, $"quick_{item.Id}", item.Name,
                            item.Entry.Type == Models.TransactionType.Income ? Resource.Drawable.ic_widget_income : Resource.Drawable.ic_widget_expense, NotificationLaunch.QuickEntryRoute(item.Id)));
                }

                var shortcuts = new List<ShortcutInfoCompat>(quick)
                {
                    Create(context, "add_expense", AppResources.Expense, Resource.Drawable.ic_widget_expense,
                        $"{BalanceWidget.AddTransactionRoute}?Type={Models.TransactionType.Expense}"),
                    Create(context, "add_income", AppResources.Income, Resource.Drawable.ic_widget_income,
                        $"{BalanceWidget.AddTransactionRoute}?Type={Models.TransactionType.Income}"),
                    Create(context, "add_transfer", AppResources.Transfer, Resource.Drawable.ic_widget_transfer,
                        BalanceWidget.AddTransferRoute),
                };
                ShortcutManagerCompat.SetDynamicShortcuts(context, shortcuts);
            }
            catch (Exception ex)
            {
                // A convenience only: the add button does the same
                IPlatformApplication.Current?.Services?.GetService<ILogger<MainActivity>>()
                    ?.LogWarning(ex, "Could not publish the launcher shortcuts");
            }
        }

        private static ShortcutInfoCompat Create(Context context, string id, string label, int icon, string route)
        {
            var intent = new Intent(context, typeof(MainActivity))
                .SetAction(Intent.ActionMain)
                .SetFlags(ActivityFlags.NewTask | ActivityFlags.SingleTop | ActivityFlags.ClearTop)
                .PutExtra(MainActivity.RouteExtra, route);
            return new ShortcutInfoCompat.Builder(context, id)
                .SetShortLabel(label)
                .SetLongLabel(label)
                .SetIcon(IconCompat.CreateWithResource(context, icon))
                .SetIntent(intent)
                .Build();
        }
    }
}
