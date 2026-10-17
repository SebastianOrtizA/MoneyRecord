using Android.App;
using Android.Appwidget;
using Android.Content;
using Android.OS;
using Microsoft.Extensions.Logging;
using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Platforms.Android
{
    /// <summary>
    /// Asks which account a new widget shows (and, on Android 12+, a placed one when the user reconfigures it): a list
    /// of the accounts over the home screen. Leaving without a pick cancels adding the widget.
    /// </summary>
    [Activity(Name = "com.sebastianortiz.moneyrecord.BalanceWidgetConfigureActivity", Exported = true,
        Theme = "@android:style/Theme.Translucent.NoTitleBar", ExcludeFromRecents = true)]
    [IntentFilter([AppWidgetManager.ActionAppwidgetConfigure])]
    public sealed class BalanceWidgetConfigureActivity : Activity
    {
        protected override async void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetResult(Result.Canceled);

            var widgetId = Intent?.Extras?.GetInt(AppWidgetManager.ExtraAppwidgetId, AppWidgetManager.InvalidAppwidgetId)
                ?? AppWidgetManager.InvalidAppwidgetId;
            var services = IPlatformApplication.Current?.Services;
            if (widgetId == AppWidgetManager.InvalidAppwidgetId || services == null)
            {
                Finish();
                return;
            }

            var widget = services.GetRequiredService<IBalanceWidget>();
            List<Account> accounts;
            try
            {
                accounts = await widget.GetAccountsAsync();
            }
            catch (Exception ex)
            {
                services.GetRequiredService<ILogger<BalanceWidgetConfigureActivity>>().LogError(ex, "Failed to load the widget's accounts");
                Finish();
                return;
            }

            // Nothing to pick: the widget shows the buttons only
            if (accounts.Count == 0)
            {
                Done(widgetId);
                Finish();
                return;
            }

            var names = accounts
                .Select(a => $"{a.Name} · {MoneyFormatter.ResolveCurrencyCode(a.CurrencyCode)}")
                .ToArray();
            // The phone's own dialog look, light or dark like the system
            var theme = OperatingSystem.IsAndroidVersionAtLeast(22)
                ? global::Android.Resource.Style.ThemeDeviceDefaultDialogAlert
                : global::Android.Resource.Style.ThemeDeviceDefaultDialog;
            var dialog = new AlertDialog.Builder(this, theme)
                .SetTitle(AppResources.WidgetPickAccount)!
                .SetItems(names, (s, e) =>
                {
                    widget.SetAccount(widgetId, accounts[e.Which].Id);
                    Done(widgetId);
                })!
                .Create()!;
            // A pick also closes the list
            dialog.DismissEvent += (s, e) => Finish();
            dialog.Show();
        }

        /// <summary>Adds (or redraws) the widget with its account.</summary>
        private void Done(int widgetId)
        {
            SetResult(Result.Ok, new Intent().PutExtra(AppWidgetManager.ExtraAppwidgetId, widgetId));
            var context = ApplicationContext ?? this;
            _ = BalanceWidgetProvider.UpdateAsync(context, AppWidgetManager.GetInstance(context)!, [widgetId]);
        }
    }
}
