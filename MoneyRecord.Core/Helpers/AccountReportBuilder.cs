using MoneyRecord.Models;

namespace MoneyRecord.Helpers
{
    /// <summary>One account of the report, in its own currency: what came in, what went out, transfers and its balance today.</summary>
    /// <param name="Transfers">Transfers in minus transfers out (signed).</param>
    public sealed record AccountReportRow(int AccountId, string Name, string CurrencyCode, decimal Income, decimal Expense,
        decimal Transfers, decimal Balance)
    {
        /// <summary>How the balance changed over the period.</summary>
        public decimal Net => Income - Expense + Transfers;
    }

    /// <summary>The accounts of one currency with their sums (amounts of different currencies are never added).</summary>
    public sealed record AccountReportGroup(string CurrencyCode, IReadOnlyList<AccountReportRow> Rows)
    {
        public decimal Income => Rows.Sum(r => r.Income);

        public decimal Expense => Rows.Sum(r => r.Expense);

        public decimal Transfers => Rows.Sum(r => r.Transfers);

        public decimal Net => Rows.Sum(r => r.Net);

        public decimal Balance => Rows.Sum(r => r.Balance);
    }

    /// <summary>Income, expense, transfers and net per account for a period, grouped by account currency.</summary>
    public static class AccountReportBuilder
    {
        /// <param name="balances">Every account's balance today, by account ID (accounts without one count 0).</param>
        /// <returns>Groups with the base currency first, then by code; accounts by name. Currencies without accounts are absent.</returns>
        public static List<AccountReportGroup> Build(IEnumerable<Account> accounts, string baseCurrencyCode,
            IEnumerable<AccountTotal> incomes, IEnumerable<AccountTotal> expenses,
            IEnumerable<AccountTotal> transfersOut, IEnumerable<AccountTotal> transfersIn,
            IReadOnlyDictionary<int, decimal> balances)
        {
            var income = incomes.ToDictionary(t => t.AccountId);
            var expense = expenses.ToDictionary(t => t.AccountId);
            var outgoing = transfersOut.ToDictionary(t => t.AccountId);
            var incoming = transfersIn.ToDictionary(t => t.AccountId);
            var baseCode = baseCurrencyCode.ToUpperInvariant();

            AccountReportRow Row(Account a) => new(a.Id, a.Name ?? string.Empty,
                MoneyFormatter.ResolveCurrencyCode(a.CurrencyCode, baseCode),
                income.GetValueOrDefault(a.Id)?.Amount ?? 0,
                expense.GetValueOrDefault(a.Id)?.Amount ?? 0,
                (incoming.GetValueOrDefault(a.Id)?.Amount ?? 0) - (outgoing.GetValueOrDefault(a.Id)?.Amount ?? 0),
                balances.GetValueOrDefault(a.Id));

            return accounts.Select(Row)
                .GroupBy(r => r.CurrencyCode)
                .OrderBy(g => g.Key == baseCode ? 0 : 1).ThenBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => new AccountReportGroup(g.Key,
                    [.. g.OrderBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase)]))
                .ToList();
        }
    }
}
