using MoneyRecord.Models;

namespace MoneyRecord.Services.Interfaces
{
    /// <summary>
    /// The home-screen widget: each one shows the balance of the account the user picked when adding it, plus Expense /
    /// Income / Transfer buttons that open the add forms on that account. The home screen is seen without unlocking the
    /// app, so the balance is hidden while Home hides balances or the app lock is on.
    /// </summary>
    public interface IBalanceWidget
    {
        /// <summary>The accounts a widget can show (the user's own, not the debts' hidden ones).</summary>
        Task<List<Account>> GetAccountsAsync();

        void SetAccount(int widgetId, int accountId);

        /// <summary>The widget was removed from the home screen.</summary>
        void Forget(int widgetId);

        /// <summary>
        /// The widget's content. An account that is gone (or was never picked) falls back to the default account, then the
        /// first one.
        /// </summary>
        Task<WidgetContent> GetContentAsync(int widgetId);
    }
}
