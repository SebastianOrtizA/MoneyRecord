using Android.App;
using Android.Appwidget;
using Android.Content;
using Android.Widget;
using Microsoft.Extensions.Logging;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Platforms.Android
{
    /// <summary>
    /// The home-screen widget (<see cref="IBalanceWidget"/>): the picked account's balance, which opens the app, and
    /// Expense / Income / Transfer buttons that open the add forms on that account (through the app lock, like a
    /// notification: <see cref="MainActivity.RouteExtra"/>). Android asks for a redraw when one is added; the app redraws
    /// them itself (<see cref="AndroidHomeScreenWidgets"/>). The fixed name keeps placed widgets working across builds.
    /// </summary>
    [BroadcastReceiver(Name = "com.sebastianortiz.moneyrecord.BalanceWidgetProvider", Label = "@string/widget_name", Exported = true)]
    [IntentFilter([AppWidgetManager.ActionAppwidgetUpdate])]
    [MetaData(AppWidgetManager.MetaDataAppwidgetProvider, Resource = "@xml/widget_balance_info")]
    public sealed class BalanceWidgetProvider : AppWidgetProvider
    {
        // Request codes per widget: the header and the three buttons need their own pending intents
        private const int IntentsPerWidget = 4;

        public override void OnUpdate(Context? context, AppWidgetManager? appWidgetManager, int[]? appWidgetIds)
        {
            if (context == null || appWidgetManager == null || appWidgetIds == null)
                return;

            // The database is read off the main thread; the broadcast stays alive until the widgets are drawn
            var pending = GoAsync();
            _ = Task.Run(async () =>
            {
                try
                {
                    await UpdateAsync(context.ApplicationContext ?? context, appWidgetManager, appWidgetIds);
                }
                finally
                {
                    pending?.Finish();
                }
            });
        }

        public override void OnDeleted(Context? context, int[]? appWidgetIds)
        {
            var widget = IPlatformApplication.Current?.Services.GetService<IBalanceWidget>();
            foreach (var id in appWidgetIds ?? [])
                widget?.Forget(id);
        }

        /// <summary>The widgets on the home screen.</summary>
        public static int[] GetIds(Context context, AppWidgetManager manager) =>
            manager.GetAppWidgetIds(new ComponentName(context, Java.Lang.Class.FromType(typeof(BalanceWidgetProvider)))) ?? [];

        /// <summary>Redraws the widgets; a failure is logged (the widget keeps what it showed).</summary>
        public static async Task UpdateAsync(Context context, AppWidgetManager manager, int[] ids)
        {
            var services = IPlatformApplication.Current?.Services;
            if (services == null)
                return;

            try
            {
                var widget = services.GetRequiredService<IBalanceWidget>();
                foreach (var id in ids)
                    manager.UpdateAppWidget(id, Build(context, id, await widget.GetContentAsync(id)));
            }
            catch (Exception ex)
            {
                services.GetRequiredService<ILogger<BalanceWidgetProvider>>().LogError(ex, "Failed to update the widget");
            }
        }

        private static RemoteViews Build(Context context, int widgetId, WidgetContent content)
        {
            var views = new RemoteViews(context.PackageName, Resource.Layout.widget_balance);
            views.SetTextViewText(Resource.Id.widget_account, content.AccountName);
            views.SetTextViewText(Resource.Id.widget_balance, content.BalanceText);
            views.SetTextViewText(Resource.Id.widget_expense_text, AppResources.Expense);
            views.SetTextViewText(Resource.Id.widget_income_text, AppResources.Income);
            views.SetTextViewText(Resource.Id.widget_transfer_text, AppResources.Transfer);

            views.SetOnClickPendingIntent(Resource.Id.widget_header, Open(context, widgetId, 0, null));
            views.SetOnClickPendingIntent(Resource.Id.widget_expense, Open(context, widgetId, 1, content.ExpenseRoute));
            views.SetOnClickPendingIntent(Resource.Id.widget_income, Open(context, widgetId, 2, content.IncomeRoute));
            views.SetOnClickPendingIntent(Resource.Id.widget_transfer, Open(context, widgetId, 3, content.TransferRoute));
            return views;
        }

        /// <summary>Opens the app, on <paramref name="route"/> when there is one (as a tapped notification does).</summary>
        private static PendingIntent Open(Context context, int widgetId, int index, string? route)
        {
            var intent = new Intent(context, typeof(MainActivity))
                .SetFlags(ActivityFlags.NewTask | ActivityFlags.SingleTop | ActivityFlags.ClearTop);
            if (route != null)
                intent.PutExtra(MainActivity.RouteExtra, route);
            return PendingIntent.GetActivity(context, widgetId * IntentsPerWidget + index, intent,
                PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent)!;
        }
    }
}
