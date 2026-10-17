using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Services
{
    public sealed class BalanceWidget(IAccountRepository accounts, IBalanceService balances, IPrivacySettings privacy,
        AppLock appLock, IPreferencesService preferences) : IBalanceWidget
    {
        /// <summary>What a hidden balance shows: the same text as Home's (`HiddenAmount` in Styles.xaml).</summary>
        public const string HiddenAmount = "••••••";

        // The app's routes (see AppShell); Core can't name the page types
        public const string AddTransactionRoute = "AddTransactionPage";
        public const string AddTransferRoute = "AddTransferPage";

        // The account per widget ID
        private const string AccountKey = "Widget.Account.";

        private bool BalancesHidden => privacy.HideBalances || appLock.CanLock;

        public Task<List<Account>> GetAccountsAsync() => accounts.GetOwnAsync();

        public void SetAccount(int widgetId, int accountId) => preferences.Set(AccountKey + widgetId, accountId);

        public void Forget(int widgetId) => preferences.Set(AccountKey + widgetId, 0);

        public async Task<WidgetContent> GetContentAsync(int widgetId)
        {
            var own = await accounts.GetOwnAsync();
            var accountId = preferences.Get(AccountKey + widgetId, 0);
            var account = own.FirstOrDefault(a => a.Id == accountId)
                ?? own.FirstOrDefault(a => a.IsDefault)
                ?? own.FirstOrDefault();

            var balanceText = string.Empty;
            if (account != null)
            {
                balanceText = BalancesHidden
                    ? HiddenAmount
                    : MoneyFormatter.Format(await balances.GetAccountBalanceAsync(account.Id), account.CurrencyCode);
            }

            var accountQuery = account == null ? string.Empty : $"&AccountId={account.Id}";
            return new WidgetContent
            {
                AccountName = account?.Name ?? string.Empty,
                BalanceText = balanceText,
                ExpenseRoute = $"{AddTransactionRoute}?Type={TransactionType.Expense}{accountQuery}",
                IncomeRoute = $"{AddTransactionRoute}?Type={TransactionType.Income}{accountQuery}",
                TransferRoute = account == null ? AddTransferRoute : $"{AddTransferRoute}?SourceAccountId={account.Id}",
            };
        }
    }
}
