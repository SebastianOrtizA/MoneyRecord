using Android.Appwidget;
using MoneyRecord.Services.Interfaces;
using Application = Android.App.Application;

namespace MoneyRecord.Platforms.Android
{
    /// <summary>Redraws the balance widgets placed on the home screen (<see cref="BalanceWidgetProvider"/>).</summary>
    public sealed class AndroidHomeScreenWidgets : IHomeScreenWidgets
    {
        public void Refresh()
        {
            var context = Application.Context;
            var manager = AppWidgetManager.GetInstance(context);
            if (manager == null)
                return;

            var ids = BalanceWidgetProvider.GetIds(context, manager);
            if (ids.Length > 0)
                _ = Task.Run(() => BalanceWidgetProvider.UpdateAsync(context, manager, ids));
        }
    }
}
