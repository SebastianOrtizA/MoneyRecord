using MoneyRecord.Helpers;
using MoneyRecord.Models;

namespace MoneyRecord.Services.Repositories
{
    public sealed class TransactionRepository(DatabaseInitializer database)
        : RepositoryBase<Transaction>(database), Interfaces.ITransactionRepository
    {
        // A transaction is in its account's currency; one without an account (or a currency) is in the base currency
        private const string ByCurrencySql =
            "SELECT t.CategoryId AS CategoryId, COALESCE(NULLIF(a.CurrencyCode, ''), ?) AS CurrencyCode, " +
            "SUM(ABS(t.AmountCents)) AS Cents " +
            "FROM \"Transaction\" t LEFT JOIN \"Account\" a ON a.Id = t.AccountId " +
            "WHERE t.Type = ? AND t.Date >= ? AND t.Date <= ? " +
            "GROUP BY t.CategoryId, COALESCE(NULLIF(a.CurrencyCode, ''), ?)";

        public async Task<List<Transaction>> GetByDateRangeAsync(DateTime startDate, DateTime endDate) =>
            await (await TableAsync())
                .Where(t => t.Date >= startDate && t.Date <= endDate)
                .ToListAsync();

        public async Task<List<CurrencyAmount>> SumByCurrencyAsync(TransactionType type, DateTime startDate, DateTime endDate,
            string baseCurrencyCode) =>
            CurrencyAmount.Totals((await SumByCategoryRowsAsync(type, startDate, endDate, baseCurrencyCode)).Select(r => r.ToAmount()),
                baseCurrencyCode);

        public async Task<Dictionary<int, List<CurrencyAmount>>> SumByCategoryAsync(TransactionType type, DateTime startDate,
            DateTime endDate, string baseCurrencyCode) =>
            (await SumByCategoryRowsAsync(type, startDate, endDate, baseCurrencyCode))
                .GroupBy(r => r.CategoryId)
                .ToDictionary(g => g.Key, g => CurrencyAmount.Totals(g.Select(r => r.ToAmount()), baseCurrencyCode));

        public async Task<List<AccountTotal>> SumCentsByAccountAsync(TransactionType type) =>
            await (await ConnectionAsync()).QueryAsync<AccountTotal>(
                "SELECT AccountId, SUM(ABS(AmountCents)) AS Cents, MAX(Date) AS LastDateTicks FROM \"Transaction\" " +
                "WHERE Type = ? AND AccountId IS NOT NULL GROUP BY AccountId",
                type);

        private async Task<List<CategoryCurrencyTotal>> SumByCategoryRowsAsync(TransactionType type, DateTime startDate,
            DateTime endDate, string baseCurrencyCode)
        {
            var code = baseCurrencyCode.ToUpperInvariant();
            return await (await ConnectionAsync()).QueryAsync<CategoryCurrencyTotal>(ByCurrencySql, code, type, startDate, endDate, code);
        }

        private sealed class CategoryCurrencyTotal
        {
            public int CategoryId { get; set; }

            public string CurrencyCode { get; set; } = string.Empty;

            public long Cents { get; set; }

            public CurrencyAmount ToAmount() => new(CurrencyCode, Money.FromCents(Cents));
        }
    }
}
