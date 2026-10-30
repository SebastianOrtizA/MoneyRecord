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

        /// <summary>The account's pockets, by name.</summary>
        public IReadOnlyList<PocketSummary> Pockets { get; init; } = [];

        public bool HasPockets => Pockets.Count > 0;

        /// <summary>What its pockets hold (set aside inside the balance).</summary>
        public decimal InPockets => Pockets.Sum(p => p.Balance);

        /// <summary>The balance minus the pockets; negative when entries took money the pockets expected to keep.</summary>
        public decimal Available => CurrentBalance - InPockets;

        public bool IsOverCommitted => HasPockets && Available < 0;

        /// <summary>"Available $1,500.00 · $500.00 in pockets"; empty without pockets.</summary>
        public string AvailableText => HasPockets
            ? string.Format(AppResources.AvailableAndInPockets,
                MoneyFormatter.Format(Available, CurrencyCode), MoneyFormatter.Format(InPockets, CurrencyCode))
            : string.Empty;
    }

    /// <summary>
    /// The accounts in one currency with their subtotal in that currency, for the grouped Accounts list
    /// (amounts in different currencies are never added together).
    /// </summary>
    public sealed class AccountGroup : List<AccountListItem>
    {
        /// <param name="all">The whole currency's accounts when <paramref name="items"/> are only those a search found, so the header still shows the currency's total.</param>
        public AccountGroup(string currencyCode, IEnumerable<AccountListItem> items, IReadOnlyCollection<AccountListItem>? all = null) : base(items)
        {
            CurrencyCode = currencyCode;
            all ??= this;
            Subtotal = all.Sum(i => i.CurrentBalance);
            AccountCount = all.Count;
        }

        public string CurrencyCode { get; }

        public decimal Subtotal { get; }

        /// <summary>How many accounts the currency has (more than are listed while a search shows only some).</summary>
        public int AccountCount { get; }

        /// <summary>"€300.00 EUR · 2 accounts" (bare sign + code, like the Home balance lines).</summary>
        public string HeaderText =>
            $"{MoneyFormatter.FormatWithCode(Subtotal, CurrencyCode)} · {Plurals.Accounts(AccountCount)}";

        /// <summary>
        /// The accounts whose name or currency code contain <paramref name="query"/> (ignoring case and accents), per currency;
        /// currencies without a match are left out and each header keeps the currency's whole total.
        /// </summary>
        public static List<AccountGroup> Filter(IEnumerable<AccountGroup> groups, string? query) =>
            groups
                .Select(g => new AccountGroup(g.CurrencyCode,
                    g.Where(i => ListSearch.Matches(i.Account.Name, query) || ListSearch.Matches(i.CurrencyCode, query)), g))
                .Where(g => g.Count > 0)
                .ToList();

        /// <summary>
        /// One group per currency, base currency first, then by code; accounts keep their given order. With
        /// <paramref name="currencyFilter"/> only that currency's group is built (empty when it has no accounts).
        /// </summary>
        public static List<AccountGroup> Build(IEnumerable<Account> accounts, IEnumerable<AccountBalanceInfo> balances,
            string baseCurrencyCode, string? currencyFilter = null, IEnumerable<PocketSummary>? pockets = null)
        {
            baseCurrencyCode = baseCurrencyCode.ToUpperInvariant();
            var balanceMap = balances.ToDictionary(b => b.AccountId);
            var pocketsByAccount = (pockets ?? []).GroupBy(p => p.Account.Id).ToDictionary(g => g.Key, g => g.ToList());

            return accounts
                .Select(account => new AccountListItem
                {
                    Account = account,
                    CurrencyCode = MoneyFormatter.ResolveCurrencyCode(account.CurrencyCode, baseCurrencyCode),
                    CurrentBalance = balanceMap.GetValueOrDefault(account.Id)?.CurrentBalance ?? account.InitialBalance,
                    LastActivityDate = balanceMap.GetValueOrDefault(account.Id)?.LastActivityDate,
                    Pockets = pocketsByAccount.GetValueOrDefault(account.Id) ?? []
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
