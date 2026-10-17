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

        public async Task<List<Transaction>> GetSplitPartsAsync(string splitId) =>
            await (await TableAsync()).Where(t => t.SplitId == splitId).OrderBy(t => t.Id).ToListAsync();

        public async Task DeleteSplitAsync(string splitId)
        {
            await (await ConnectionAsync()).ExecuteAsync("DELETE FROM \"Transaction\" WHERE SplitId = ?", splitId);
            OnChanged();
        }

        public async Task<List<CurrencyAmount>> SumByCurrencyAsync(TransactionType type, DateTime startDate, DateTime endDate,
            string baseCurrencyCode) =>
            CurrencyAmount.Totals((await SumByCategoryRowsAsync(type, startDate, endDate, baseCurrencyCode)).Select(r => r.ToAmount()),
                baseCurrencyCode);

        public async Task<Dictionary<int, List<CurrencyAmount>>> SumByCategoryAsync(TransactionType type, DateTime startDate,
            DateTime endDate, string baseCurrencyCode) =>
            (await SumByCategoryRowsAsync(type, startDate, endDate, baseCurrencyCode))
                .GroupBy(r => r.CategoryId)
                .ToDictionary(g => g.Key, g => CurrencyAmount.Totals(g.Select(r => r.ToAmount()), baseCurrencyCode));

        public async Task<List<DailyTotal>> SumByDayAsync(DateTime startDate, DateTime endDate, string baseCurrencyCode)
        {
            var code = baseCurrencyCode.ToUpperInvariant();
            // Entries with their own conversion into the base are summed as such; the rest per currency and day, for a rate
            const string ownConversion = "t.ConvertedAmountCents IS NOT NULL AND UPPER(COALESCE(t.ConvertedCurrencyCode, '')) = ?";
            return await (await ConnectionAsync()).QueryAsync<DailyTotal>(
                "SELECT t.Type AS Type, t.CategoryId AS CategoryId, COALESCE(NULLIF(a.CurrencyCode, ''), ?) AS CurrencyCode, " +
                $"t.Date / {TimeSpan.TicksPerDay} AS Day, SUM(ABS(t.AmountCents)) AS Cents, 0 AS IsOwnConversion " +
                "FROM \"Transaction\" t LEFT JOIN \"Account\" a ON a.Id = t.AccountId " +
                $"WHERE t.Date >= ? AND t.Date <= ? AND NOT ({ownConversion}) " +
                "GROUP BY t.Type, t.CategoryId, COALESCE(NULLIF(a.CurrencyCode, ''), ?), t.Date / " + TimeSpan.TicksPerDay + " " +
                "UNION ALL " +
                "SELECT t.Type, t.CategoryId, ?, 0, SUM(ABS(t.ConvertedAmountCents)), 1 FROM \"Transaction\" t " +
                $"WHERE t.Date >= ? AND t.Date <= ? AND {ownConversion} GROUP BY t.Type, t.CategoryId",
                code, startDate, endDate, code, code, code, startDate, endDate, code);
        }

        public async Task<Dictionary<string, DateTime>> GetFirstDatesByCurrencyAsync(string baseCurrencyCode)
        {
            var code = baseCurrencyCode.ToUpperInvariant();
            var rows = await (await ConnectionAsync()).QueryAsync<CurrencyFirstDate>(
                "SELECT COALESCE(NULLIF(a.CurrencyCode, ''), ?) AS CurrencyCode, MIN(t.Date) AS FirstTicks " +
                "FROM \"Transaction\" t LEFT JOIN \"Account\" a ON a.Id = t.AccountId " +
                "GROUP BY COALESCE(NULLIF(a.CurrencyCode, ''), ?)", code, code);
            return rows
                .GroupBy(r => r.CurrencyCode.ToUpperInvariant())
                .ToDictionary(g => g.Key, g => new DateTime(g.Min(r => r.FirstTicks)));
        }

        public async Task<List<AccountTotal>> SumCentsByAccountAsync(TransactionType type, DateTime? since = null,
            DateTime? before = null)
        {
            var (where, args) = DateFilter.Sql(since, before);
            return await (await ConnectionAsync()).QueryAsync<AccountTotal>(
                "SELECT AccountId, SUM(ABS(AmountCents)) AS Cents, MAX(Date) AS LastDateTicks FROM \"Transaction\" " +
                $"WHERE Type = ? AND AccountId IS NOT NULL{where} GROUP BY AccountId", [type, .. args]);
        }

        private async Task<List<CategoryCurrencyTotal>> SumByCategoryRowsAsync(TransactionType type, DateTime startDate,
            DateTime endDate, string baseCurrencyCode)
        {
            var code = baseCurrencyCode.ToUpperInvariant();
            return await (await ConnectionAsync()).QueryAsync<CategoryCurrencyTotal>(ByCurrencySql, code, type, startDate, endDate, code);
        }

        private sealed class CurrencyFirstDate
        {
            public string CurrencyCode { get; set; } = string.Empty;

            public long FirstTicks { get; set; }
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
