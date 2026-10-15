using MoneyRecord.Models;

namespace MoneyRecord.Helpers
{
    /// <summary>
    /// The reports show one currency at a time (until exchange rates exist, Stage 10.3b): which currencies there are,
    /// and the transactions in one of them.
    /// </summary>
    public static class ReportCurrencies
    {
        /// <summary>The base currency first (always listed), then every other account currency by code.</summary>
        public static List<string> InUse(IEnumerable<Account> accounts, string baseCurrencyCode)
        {
            baseCurrencyCode = baseCurrencyCode.ToUpperInvariant();
            return accounts
                .Select(a => MoneyFormatter.ResolveCurrencyCode(a.CurrencyCode, baseCurrencyCode))
                .Append(baseCurrencyCode)
                .Distinct()
                .OrderBy(code => code == baseCurrencyCode ? 0 : 1)
                .ThenBy(code => code, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// The transactions in <paramref name="currencyCode"/>: their account's currency, or the base currency for one
        /// without an account (or whose account is gone).
        /// </summary>
        public static List<Transaction> Only(IEnumerable<Transaction> transactions, IEnumerable<Account> accounts,
            string currencyCode, string baseCurrencyCode)
        {
            var codes = accounts.ToDictionary(a => a.Id, a => MoneyFormatter.ResolveCurrencyCode(a.CurrencyCode, baseCurrencyCode));
            var wanted = currencyCode.ToUpperInvariant();
            var fallback = baseCurrencyCode.ToUpperInvariant();
            return transactions
                .Where(t => (t.AccountId is { } id && codes.TryGetValue(id, out var code) ? code : fallback) == wanted)
                .ToList();
        }
    }
}
