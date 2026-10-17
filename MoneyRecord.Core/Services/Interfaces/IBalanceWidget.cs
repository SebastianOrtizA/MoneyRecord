using MoneyRecord.Models;

namespace MoneyRecord.Services.Interfaces
{
    /// <summary>
    /// The home-screen widget: each one shows the balance of the account the user picked when adding it, plus Expense /
    /// Income / Transfer buttons that open the add forms on that account.
    /// </summary>
    public interface IBalanceWidget
    {
        /// <summary>
        /// The home screen is seen without unlocking the app: the balance is hidden while Home hides balances or the app
        /// lock is on.
        /// </summary>
        bool BalancesHidden { get; }

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

    /// <summary>Redraws the widgets on the home screen (Android); nothing elsewhere.</summary>
    public interface IHomeScreenWidgets
    {
        /// <summary>Best-effort and in the background: a failure is logged, never thrown.</summary>
        void Refresh();
    }
}
