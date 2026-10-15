using MoneyRecord.Helpers;

namespace MoneyRecord.Models
{
    /// <summary>A total in one currency (a resolved ISO code, never empty). Totals in different currencies are never added.</summary>
    public sealed record CurrencyAmount(string CurrencyCode, decimal Amount)
    {
        /// <summary>The amount in <paramref name="currencyCode"/> (0 when there is none).</summary>
        public static decimal In(IEnumerable<CurrencyAmount> amounts, string currencyCode) =>
            amounts.Where(a => string.Equals(a.CurrencyCode, currencyCode, StringComparison.OrdinalIgnoreCase)).Sum(a => a.Amount);

        /// <summary>The amounts in every currency but <paramref name="currencyCode"/>.</summary>
        public static List<CurrencyAmount> Except(IEnumerable<CurrencyAmount> amounts, string currencyCode) =>
            amounts.Where(a => !string.Equals(a.CurrencyCode, currencyCode, StringComparison.OrdinalIgnoreCase)).ToList();

        /// <summary>
        /// Groups by currency (empty = <paramref name="baseCurrencyCode"/>) and sums each: base currency first, then by
        /// code; currencies that add up to zero are dropped.
        /// </summary>
        public static List<CurrencyAmount> Totals(IEnumerable<CurrencyAmount> amounts, string baseCurrencyCode)
        {
            baseCurrencyCode = baseCurrencyCode.ToUpperInvariant();
            return amounts
                .GroupBy(a => MoneyFormatter.ResolveCurrencyCode(a.CurrencyCode, baseCurrencyCode))
                .Select(g => new CurrencyAmount(g.Key, g.Sum(a => a.Amount)))
                .Where(a => a.Amount != 0)
                .OrderBy(a => a.CurrencyCode == baseCurrencyCode ? 0 : 1)
                .ThenBy(a => a.CurrencyCode, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// "$1,200.00 · US$50.00": each amount in its currency; no amounts is a zero in the base currency.
        /// </summary>
        public static string Format(IReadOnlyCollection<CurrencyAmount> amounts, string separator = " · ") =>
            amounts.Count == 0
                ? MoneyFormatter.Format(0)
                : string.Join(separator, amounts.Select(a => MoneyFormatter.Format(a.Amount, a.CurrencyCode)));
    }
}
