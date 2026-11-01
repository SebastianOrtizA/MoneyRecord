using Android.Appwidget;
using MoneyRecord.Services.Interfaces;
using Application = Android.App.Application;

namespace MoneyRecord.Platforms.Android
{
    /// <summary>Redraws the balance widgets placed on the home screen (<see cref="BalanceWidgetProvider"/>) and the launcher shortcuts.</summary>
    public sealed class AndroidHomeScreenWidgets : IHomeScreenWidgets
    {
        public Task RefreshAsync()
        {
            var context = Application.Context;
            // The launcher's long-press menu, in the app's language (this also runs after a language change)
            AppShortcuts.Publish(context);
            var manager = AppWidgetManager.GetInstance(context);
            if (manager == null)
                return Task.CompletedTask;

            var ids = BalanceWidgetProvider.GetIds(context, manager);
            // Off the calling thread (the app's main thread when it goes to the background)
            return ids.Length == 0
                ? Task.CompletedTask
                : Task.Run(() => BalanceWidgetProvider.UpdateAsync(context, manager, ids));
        }
    }
}
