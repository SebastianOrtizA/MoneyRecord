using MoneyRecord.Helpers;

namespace MoneyRecord.Models
{
    /// <summary>
    /// The total balance of all accounts in one currency (amounts in different currencies are never added together).
    /// </summary>
    public sealed record CurrencyBalance(string CurrencyCode, decimal Balance, int AccountCount)
    {
        /// <summary>
        /// One row per currency, base currency first, from already computed account balances (so a caller that also
        /// needs the per-account balances computes them only once). An account without a currency counts as the base.
        /// </summary>
        public static List<CurrencyBalance> FromAccounts(IEnumerable<AccountBalanceInfo> balances, string baseCurrencyCode)
        {
            baseCurrencyCode = baseCurrencyCode.ToUpperInvariant();
            return balances
                .GroupBy(b => MoneyFormatter.ResolveCurrencyCode(b.CurrencyCode, baseCurrencyCode))
                .Select(g => new CurrencyBalance(g.Key, g.Sum(b => b.CurrentBalance), g.Count()))
                .OrderBy(c => c.CurrencyCode == baseCurrencyCode ? 0 : 1)
                .ThenBy(c => c.CurrencyCode, StringComparer.Ordinal)
                .ToList();
        }
    }
}
