using MoneyRecord.Models;

namespace MoneyRecord.Helpers
{
    /// <summary>Filters, sorts and lists the filter options of the Transactions tab's ledger entries.</summary>
    public static class LedgerFilter
    {
        /// <summary>The entries matching <paramref name="filter"/>, by date (newest first unless <see cref="TransactionFilter.Ascending"/>).</summary>
        public static List<LedgerEntry> Apply(IEnumerable<LedgerEntry> entries, TransactionFilter filter)
        {
            var filtered = entries;

            if (!string.IsNullOrWhiteSpace(filter.SearchText))
            {
                var search = filter.SearchText.Trim();
                filtered = filtered.Where(e =>
                    Contains(e.Description, search) ||
                    Contains(e.CategoryName, search) ||
                    Contains(e.AccountName, search) ||
                    (e.IsTransfer && Contains(e.CounterpartAccountName, search)) ||
                    Contains(e.Tags, search));
            }

            if (filter.Type is TransactionType type)
                filtered = filtered.Where(e => e.Type == type);

            if (filter.CategoryId is int categoryId)
                filtered = filtered.Where(e => e.CategoryId == categoryId);

            if (filter.AccountId is int accountId)
            {
                // A whole transfer belongs to both accounts; a transfer half only to its own
                filtered = filtered.Where(e => e.AccountId == accountId ||
                                               (e.Kind == LedgerEntryKind.Transfer && e.CounterpartAccountId == accountId));
            }

            if (!string.IsNullOrWhiteSpace(filter.Tag))
            {
                var tag = filter.Tag.Trim();
                filtered = filtered.Where(e => e.TagList.Contains(tag, StringComparer.OrdinalIgnoreCase));
            }

            if (filter.MinAmount is decimal min)
                filtered = filtered.Where(e => e.Amount >= min);

            if (filter.MaxAmount is decimal max)
                filtered = filtered.Where(e => e.Amount <= max);

            return filter.Ascending
                ? filtered.OrderBy(e => e.Date).ToList()
                : filtered.OrderByDescending(e => e.Date).ToList();
        }

        /// <summary>The categories used by the entries, by name (transfers are filtered by type, not category).</summary>
        public static List<FilterOption> CategoryOptions(IEnumerable<LedgerEntry> entries) =>
            Distinct(entries
                .Where(e => e.CategoryId.HasValue)
                .Select(e => new FilterOption(e.CategoryId!.Value, e.CategoryName)));

        /// <summary>The accounts used by the entries, transfer destinations included, by name.</summary>
        public static List<FilterOption> AccountOptions(IEnumerable<LedgerEntry> entries) =>
            Distinct(entries.SelectMany(AccountsOf));

        /// <summary>The tags used by the entries, once each (ignoring case), alphabetically.</summary>
        public static List<string> TagOptions(IEnumerable<LedgerEntry> entries) =>
            entries
                .SelectMany(e => e.TagList)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.CurrentCultureIgnoreCase)
                .ToList();

        private static IEnumerable<FilterOption> AccountsOf(LedgerEntry entry)
        {
            if (entry.AccountId is int id)
                yield return new FilterOption(id, entry.AccountName);
            if (entry.CounterpartAccountId is int counterpartId)
                yield return new FilterOption(counterpartId, entry.CounterpartAccountName);
        }

        private static List<FilterOption> Distinct(IEnumerable<FilterOption> options) =>
            options
                .Where(o => !string.IsNullOrEmpty(o.Name))
                .DistinctBy(o => o.Id)
                .OrderBy(o => o.Name)
                .ToList();

        private static bool Contains(string? text, string search) =>
            text?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false;
    }
}
