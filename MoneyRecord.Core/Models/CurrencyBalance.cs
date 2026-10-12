using MoneyRecord.Helpers;

namespace MoneyRecord.Models
{
    /// <summary>One line of the Home balance card; tapping it opens the accounts in <see cref="CurrencyCode"/> (null = all).</summary>
    public sealed record BalanceLine(string Text, string? CurrencyCode);

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

        /// <summary>
        /// The Home balance card's lines: one currency is just its total; several are "€300.00 EUR", one line each,
        /// in the order given (base currency first). Never empty: no accounts shows a zero. A line's currency is what
        /// a tap filters by, so with one currency (or none) it is null: the amount opens all accounts.
        /// </summary>
        public static List<BalanceLine> DisplayLines(IReadOnlyList<CurrencyBalance> balances) => balances.Count switch
        {
            0 => [new BalanceLine(MoneyFormatter.Format(0), null)],
            1 => [new BalanceLine(MoneyFormatter.Format(balances[0].Balance, balances[0].CurrencyCode), null)],
            _ => balances.Select(b => new BalanceLine(
                $"{MoneyFormatter.Format(b.Balance, b.CurrencyCode)} {b.CurrencyCode}", b.CurrencyCode)).ToList()
        };
    }
}
