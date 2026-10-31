using MoneyRecord.Models;

namespace MoneyRecord.Helpers
{
    /// <summary>Groups the ledger entries by day, category or account.</summary>
    public static class LedgerGrouping
    {
        /// <summary>
        /// Category groups total their entries; account groups show the account's balance in its own currency
        /// (from <paramref name="balances"/>, 0 when it has none). Groups are keyed by ID (transfers, which have no
        /// category, form one group) and sorted by name. Day groups hold one date each, open, with the day's net per currency
        /// (transfers move money between own accounts and are left out of it), newest first unless <paramref name="ascending"/>.
        /// </summary>
        public static List<TransactionGroup> Group(
            IEnumerable<LedgerEntry> entries,
            GroupingMode mode,
            IReadOnlyCollection<AccountBalanceInfo>? balances = null,
            IReadOnlyCollection<Account>? accounts = null,
            bool ascending = false)
        {
            if (mode == GroupingMode.Date)
                return DayGroups(entries, ascending);

            var groups = mode switch
            {
                GroupingMode.Category => entries
                    .Where(e => !string.IsNullOrEmpty(e.CategoryName))
                    .GroupBy(e => (e.CategoryId, e.CategoryName))
                    .Select(g => CategoryGroup(g.Key.CategoryName, g.ToList())),
                GroupingMode.Account => entries
                    .Where(e => !string.IsNullOrEmpty(e.AccountName))
                    .GroupBy(e => (e.AccountId, e.AccountName))
                    .Select(g => AccountGroup(g.Key.AccountId, g.Key.AccountName, g.ToList(), balances, accounts)),
                _ => []
            };

            return groups.OrderBy(g => g.GroupName).ToList();
        }

        private static List<TransactionGroup> DayGroups(IEnumerable<LedgerEntry> entries, bool ascending)
        {
            var days = entries.GroupBy(e => e.Date.Date);
            var ordered = ascending ? days.OrderBy(g => g.Key) : days.OrderByDescending(g => g.Key);

            return ordered.Select(day =>
            {
                var items = day.ToList();
                var totals = CurrencyAmount.Totals(
                    items.Where(e => !e.IsTransfer).Select(e => new CurrencyAmount(e.CurrencyCode, e.SignedAmount)),
                    MoneyFormatter.BaseCurrency);
                var type = totals.Count == 0 ? TransactionType.Transfer
                    : totals[0].Amount >= 0 ? TransactionType.Income : TransactionType.Expense;

                return new TransactionGroup(DateFormatter.Format(day.Key, DateStyle.Long), DayIconCode, type, totals, items,
                    totalsAreSigned: true, isExpanded: true);
            }).ToList();
        }

        // calendar
        private const string DayIconCode = "F00ED";

        /// <summary>One category means one type: an income or expense category, or the transfers.</summary>
        private static TransactionGroup CategoryGroup(string name, List<LedgerEntry> items)
        {
            // One total per currency (a transfer counts in its source currency)
            var totals = CurrencyAmount.Totals(items.Select(e => new CurrencyAmount(e.CurrencyCode, e.Amount)),
                MoneyFormatter.BaseCurrency);

            return new TransactionGroup(name, items[0].CategoryIconCode, items[0].Type, totals, items);
        }

        private static TransactionGroup AccountGroup(int? accountId, string name, List<LedgerEntry> items,
            IReadOnlyCollection<AccountBalanceInfo>? balances, IReadOnlyCollection<Account>? accounts)
        {
            // The account's own currency: a transfer row from another currency must not change it
            var balance = balances?.FirstOrDefault(b => b.AccountId == accountId);
            var total = balance?.CurrentBalance ?? 0;
            var currencyCode = MoneyFormatter.ResolveCurrencyCode(balance?.CurrencyCode);
            var iconCode = accounts?.FirstOrDefault(a => a.Id == accountId)?.IconCode ?? IconHelper.DefaultAccountIconCode;
            var type = total >= 0 ? TransactionType.Income : TransactionType.Expense;

            return new TransactionGroup(name, iconCode, type, [new CurrencyAmount(currencyCode, total)], items);
        }
    }
}
