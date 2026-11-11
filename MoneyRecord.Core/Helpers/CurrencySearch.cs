using MoneyRecord.Models;

namespace MoneyRecord.Helpers
{
    /// <summary>The search box of the currency picker: which currencies match what is typed.</summary>
    public static class CurrencySearch
    {
        /// <summary>
        /// The currencies whose code, name or symbol contain the typed text (case and accents ignored), in their order;
        /// a code that starts with the text comes first ("us" lists USD before Australian Dollar).
        /// </summary>
        public static List<Currency> Filter(IEnumerable<Currency> currencies, string? query)
        {
            var typed = PayeeNames.Normalize(query);
            var matches = currencies
                .Where(c => ListSearch.Matches(c.Code, typed) || ListSearch.Matches(c.Name, typed) || ListSearch.Matches(c.Symbol, typed))
                .ToList();
            if (typed.Length == 0)
                return matches;

            return matches
                .OrderByDescending(c => c.Code.StartsWith(typed, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }
}
