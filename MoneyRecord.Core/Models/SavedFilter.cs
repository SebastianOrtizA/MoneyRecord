using System.Text.Json;

namespace MoneyRecord.Models
{
    /// <summary>
    /// A named Transactions filter (everything but the sort order and the period). Categories, account and payee keep their
    /// name beside the ID so the filter reads right in a period where no entry uses them.
    /// </summary>
    public sealed record SavedFilter(
        string Name,
        string? SearchText = null,
        TransactionType? Type = null,
        List<FilterOption>? Categories = null,
        FilterOption? Account = null,
        decimal? MinAmount = null,
        decimal? MaxAmount = null,
        string? Tag = null,
        FilterOption? Payee = null)
    {
        /// <summary>The most presets kept; the Save button refuses more.</summary>
        public const int MaxCount = 20;

        /// <summary>The filter this preset applies, sorted as asked.</summary>
        public TransactionFilter ToFilter(bool ascending) => new(
            SearchText,
            Type,
            Categories is { Count: > 0 } ? Categories.Select(c => c.Id).ToList() : null,
            Account?.Id,
            MinAmount,
            MaxAmount,
            ascending,
            Tag,
            Payee?.Id);

        /// <summary>True when <paramref name="filter"/> narrows the list exactly as this preset does (the sort order is not compared).</summary>
        public bool Matches(TransactionFilter filter) =>
            string.Equals(SearchText?.Trim() ?? string.Empty, filter.SearchText?.Trim() ?? string.Empty, StringComparison.OrdinalIgnoreCase) &&
            Type == filter.Type &&
            (Categories?.Select(c => c.Id) ?? []).Order().SequenceEqual((filter.CategoryIds ?? []).Order()) &&
            Account?.Id == filter.AccountId &&
            MinAmount == filter.MinAmount &&
            MaxAmount == filter.MaxAmount &&
            string.Equals(Tag, filter.Tag, StringComparison.OrdinalIgnoreCase) &&
            Payee?.Id == filter.PayeeId;
    }

    /// <summary>The saved filters as one JSON text (kept in preferences), and the rules for editing the list.</summary>
    public static class SavedFilters
    {
        public static string Serialize(IEnumerable<SavedFilter> filters) => JsonSerializer.Serialize(filters);

        /// <summary>Unreadable or empty text (never saved, or from another version) is no presets; nameless ones are dropped; the cap holds.</summary>
        public static List<SavedFilter> Parse(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return [];

            try
            {
                return (JsonSerializer.Deserialize<List<SavedFilter>>(json) ?? [])
                    .Where(f => !string.IsNullOrWhiteSpace(f?.Name))
                    .Take(SavedFilter.MaxCount)
                    .ToList();
            }
            catch (JsonException)
            {
                return [];
            }
        }

        /// <summary>
        /// Adds <paramref name="filter"/>, or replaces the preset with the same name (ignoring case). False, and the list
        /// unchanged, when it would be a 21st.
        /// </summary>
        public static bool TryAdd(List<SavedFilter> list, SavedFilter filter)
        {
            var existing = list.FindIndex(f => SameName(f.Name, filter.Name));
            if (existing >= 0)
            {
                list[existing] = filter;
                return true;
            }

            if (list.Count >= SavedFilter.MaxCount)
                return false;

            list.Add(filter);
            return true;
        }

        /// <summary>The preset gets <paramref name="newName"/>; false when another preset has that name already.</summary>
        public static bool TryRename(List<SavedFilter> list, SavedFilter filter, string newName)
        {
            if (list.Any(f => !ReferenceEquals(f, filter) && SameName(f.Name, newName)))
                return false;

            var index = list.IndexOf(filter);
            if (index < 0)
                return false;

            list[index] = filter with { Name = newName };
            return true;
        }

        private static bool SameName(string a, string b) => string.Equals(a.Trim(), b.Trim(), StringComparison.CurrentCultureIgnoreCase);
    }
}
