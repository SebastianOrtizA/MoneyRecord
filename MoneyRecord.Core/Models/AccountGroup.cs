using MoneyRecord.Helpers;
using MoneyRecord.Resources.Strings;

namespace MoneyRecord.Models
{
    /// <summary>One account card on the Accounts screen: its balance in its own currency and its last activity.</summary>
    public sealed class AccountListItem
    {
        public required Account Account { get; init; }

        public decimal CurrentBalance { get; init; }

        /// <summary>The account's currency; an account without one is in the base currency.</summary>
        public required string CurrencyCode { get; init; }

        public DateTime? LastActivityDate { get; init; }

        /// <summary>"Last activity: Oct 11, 2026", localized; empty without a date.</summary>
        public string LastActivityText => LastActivityDate is { } date
            ? string.Format(AppResources.LastActivityDate, DateFormatter.Format(date, DateStyle.Medium))
            : string.Empty;
    }

    /// <summary>
    /// The accounts in one currency with their subtotal in that currency, for the grouped Accounts list
    /// (amounts in different currencies are never added together).
    /// </summary>
    public sealed class AccountGroup : List<AccountListItem>
    {
        public AccountGroup(string currencyCode, IEnumerable<AccountListItem> items) : base(items)
        {
            CurrencyCode = currencyCode;
            Subtotal = this.Sum(i => i.CurrentBalance);
        }

        public string CurrencyCode { get; }

        public decimal Subtotal { get; }

        /// <summary>"EUR · €300.00 · 2 accounts".</summary>
        public string HeaderText =>
            $"{CurrencyCode} · {MoneyFormatter.Format(Subtotal, CurrencyCode)} · {Plurals.Accounts(Count)}";

        /// <summary>
        /// One group per currency, base currency first, then by code; accounts keep their given order. With
        /// <paramref name="currencyFilter"/> only that currency's group is built (empty when it has no accounts).
        /// </summary>
        public static List<AccountGroup> Build(IEnumerable<Account> accounts, IEnumerable<AccountBalanceInfo> balances,
            string baseCurrencyCode, string? currencyFilter = null)
        {
            baseCurrencyCode = baseCurrencyCode.ToUpperInvariant();
            var balanceMap = balances.ToDictionary(b => b.AccountId);

            return accounts
                .Select(account => new AccountListItem
                {
                    Account = account,
                    CurrencyCode = MoneyFormatter.ResolveCurrencyCode(account.CurrencyCode, baseCurrencyCode),
                    CurrentBalance = balanceMap.GetValueOrDefault(account.Id)?.CurrentBalance ?? account.InitialBalance,
                    LastActivityDate = balanceMap.GetValueOrDefault(account.Id)?.LastActivityDate
                })
                .Where(item => string.IsNullOrEmpty(currencyFilter) || item.CurrencyCode == currencyFilter)
                .GroupBy(item => item.CurrencyCode)
                .OrderBy(g => g.Key == baseCurrencyCode ? 0 : 1)
                .ThenBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => new AccountGroup(g.Key, g))
                .ToList();
        }
    }
}
