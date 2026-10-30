using System.Globalization;

namespace MoneyRecord.Helpers
{
    /// <summary>The search box above a list (Payees, Categories, Accounts): which rows match what is typed.</summary>
    public static class ListSearch
    {
        /// <summary>A list shows its search box from this many rows on; shorter lists stay clean.</summary>
        public const int MinItemsForSearch = 8;

        private const CompareOptions Loose = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

        /// <summary>The list is long enough to need a search box (more than <see cref="MinItemsForSearch"/> rows).</summary>
        public static bool ShowsSearch(int itemCount) => itemCount > MinItemsForSearch;

        /// <summary>The text contains what is typed, ignoring case and accents; empty text matches everything.</summary>
        public static bool Matches(string? text, string? query) =>
            PayeeNames.Normalize(query) is var typed
            && (typed.Length == 0 || CultureInfo.CurrentCulture.CompareInfo.IndexOf(text ?? string.Empty, typed, Loose) >= 0);
    }
}
