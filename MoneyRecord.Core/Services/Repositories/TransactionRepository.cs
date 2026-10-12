using MoneyRecord.Models;

namespace MoneyRecord.Services.Repositories
{
    public sealed class TransactionRepository(DatabaseInitializer database)
        : RepositoryBase<Transaction>(database), Interfaces.ITransactionRepository
    {
        public async Task<List<Transaction>> GetByDateRangeAsync(DateTime startDate, DateTime endDate) =>
            await (await TableAsync())
                .Where(t => t.Date >= startDate && t.Date <= endDate)
                .ToListAsync();

        public async Task<long> SumCentsAsync(TransactionType type, DateTime startDate, DateTime endDate, int? categoryId = null)
        {
            var db = await ConnectionAsync();
            return categoryId is int id
                ? await db.ExecuteScalarAsync<long>(
                    "SELECT COALESCE(SUM(ABS(AmountCents)), 0) FROM \"Transaction\" WHERE Type = ? AND Date >= ? AND Date <= ? AND CategoryId = ?",
                    type, startDate, endDate, id)
                : await db.ExecuteScalarAsync<long>(
                    "SELECT COALESCE(SUM(ABS(AmountCents)), 0) FROM \"Transaction\" WHERE Type = ? AND Date >= ? AND Date <= ?",
                    type, startDate, endDate);
        }

        public async Task<Dictionary<int, long>> SumCentsByCategoryAsync(TransactionType type, DateTime startDate, DateTime endDate)
        {
            var rows = await (await ConnectionAsync()).QueryAsync<CategoryTotal>(
                "SELECT CategoryId, SUM(ABS(AmountCents)) AS Cents FROM \"Transaction\" " +
                "WHERE Type = ? AND Date >= ? AND Date <= ? GROUP BY CategoryId",
                type, startDate, endDate);
            return rows.ToDictionary(r => r.CategoryId, r => r.Cents);
        }

        public async Task<List<AccountTotal>> SumCentsByAccountAsync(TransactionType type) =>
            await (await ConnectionAsync()).QueryAsync<AccountTotal>(
                "SELECT AccountId, SUM(ABS(AmountCents)) AS Cents, MAX(Date) AS LastDateTicks FROM \"Transaction\" " +
                "WHERE Type = ? AND AccountId IS NOT NULL GROUP BY AccountId",
                type);

        private sealed class CategoryTotal
        {
            public int CategoryId { get; set; }

            public long Cents { get; set; }
        }
    }
}
