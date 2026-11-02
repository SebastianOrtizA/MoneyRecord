using Android.Content;
using AColor = Android.Graphics.Color;
using APaint = Android.Graphics.Paint;
using ARect = Android.Graphics.Rect;
using Bitmap = Android.Graphics.Bitmap;
using Canvas = Android.Graphics.Canvas;
using Typeface = Android.Graphics.Typeface;
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
                            CategoryIcon(context, item.CategoryDisplayIcon), NotificationLaunch.QuickEntryRoute(item.Id)));
                }

                var shortcuts = new List<ShortcutInfoCompat>(quick)
                {
                    Create(context, "add_expense", AppResources.Expense, Resource.Drawable.ic_shortcut_expense,
                        $"{BalanceWidget.AddTransactionRoute}?Type={Models.TransactionType.Expense}"),
                    Create(context, "add_income", AppResources.Income, Resource.Drawable.ic_shortcut_income,
                        $"{BalanceWidget.AddTransactionRoute}?Type={Models.TransactionType.Income}"),
                    Create(context, "add_transfer", AppResources.Transfer, Resource.Drawable.ic_shortcut_transfer,
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

        private static ShortcutInfoCompat Create(Context context, string id, string label, int icon, string route) =>
            Create(context, id, label, IconCompat.CreateWithResource(context, icon), route);

        /// <summary>The category's glyph (Material Design Icons font), white on the app's purple; the bolt when it can't be drawn.</summary>
        private static IconCompat CategoryIcon(Context context, string glyph)
        {
            try
            {
                // An adaptive icon: the launcher crops the 108dp square to its own shape and shows the middle 66dp
                const int size = 216;
                var bitmap = Bitmap.CreateBitmap(size, size, Bitmap.Config.Argb8888!)!;
                var canvas = new Canvas(bitmap);
                canvas.DrawColor(AColor.ParseColor("#512BD4"));
                using var paint = new APaint(global::Android.Graphics.PaintFlags.AntiAlias)
                {
                    Color = AColor.White,
                    TextSize = 96,
                    TextAlign = APaint.Align.Center,
                };
                paint.SetTypeface(Typeface.CreateFromAsset(context.Assets, "materialdesignicons-webfont.ttf"));
                var bounds = new ARect();
                paint.GetTextBounds(glyph, 0, glyph.Length, bounds);
                canvas.DrawText(glyph, size / 2f, size / 2f - bounds.ExactCenterY(), paint);
                return IconCompat.CreateWithAdaptiveBitmap(bitmap);
            }
            catch (Exception ex)
            {
                IPlatformApplication.Current?.Services?.GetService<ILogger<MainActivity>>()?.LogWarning(ex, "Could not draw a category shortcut icon");
                return IconCompat.CreateWithResource(context, Resource.Drawable.ic_shortcut_quick);
            }
        }

        private static ShortcutInfoCompat Create(Context context, string id, string label, IconCompat icon, string route)
        {
            var intent = new Intent(context, typeof(MainActivity))
                .SetAction(Intent.ActionMain)
                .SetFlags(ActivityFlags.NewTask | ActivityFlags.SingleTop | ActivityFlags.ClearTop)
                .PutExtra(MainActivity.RouteExtra, route);
            return new ShortcutInfoCompat.Builder(context, id)
                .SetShortLabel(label)
                .SetLongLabel(label)
                .SetIcon(icon)
                .SetIntent(intent)
                .Build();
        }
    }
}
