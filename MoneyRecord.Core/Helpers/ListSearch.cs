using System.Globalization;

namespace MoneyRecord.Helpers
{
    /// <summary>The search box above a list (Payees, Categories, Accounts): which rows match what is typed.</summary>
    public static class ListSearch
    {
        /// <summary>A list this long or shorter shows no search box; longer ones do.</summary>
        public const int MaxItemsWithoutSearch = 8;

        /// <summary>Case and accents don't matter.</summary>
        internal const CompareOptions Loose = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

        /// <summary>The list is long enough to need a search box (more than <see cref="MaxItemsWithoutSearch"/> rows).</summary>
        public static bool ShowsSearch(int itemCount) => itemCount > MaxItemsWithoutSearch;

        /// <summary>
        /// What to filter by: the typed text, or nothing when the list is too short for a search box (it shrank since
        /// something was typed, and the box is gone).
        /// </summary>
        public static string EffectiveQuery(int itemCount, string? typed) => ShowsSearch(itemCount) ? typed ?? string.Empty : string.Empty;

        /// <summary>The text contains what is typed, ignoring case and accents; empty text matches everything.</summary>
        public static bool Matches(string? text, string? query)
        {
            var typed = PayeeNames.Normalize(query);
            return typed.Length == 0 || CultureInfo.CurrentCulture.CompareInfo.IndexOf(text ?? string.Empty, typed, Loose) >= 0;
        }
    }
}
