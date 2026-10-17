using MoneyRecord.Models;

namespace MoneyRecord.Services.Repositories
{
    public sealed class TransferRepository(DatabaseInitializer database)
        : RepositoryBase<Transfer>(database), Interfaces.ITransferRepository
    {
        public async Task<List<Transfer>> GetByDateRangeAsync(DateTime startDate, DateTime endDate) =>
            await (await TableAsync())
                .Where(t => t.Date >= startDate && t.Date <= endDate)
                .ToListAsync();

        public Task<List<AccountTotal>> SumCentsBySourceAsync(DateTime? since = null, DateTime? before = null) =>
            SumCentsByAsync("SourceAccountId", "AmountCents", since, before);

        // The destination receives its own amount (another currency's, for a cross-currency transfer)
        public Task<List<AccountTotal>> SumCentsByDestinationAsync(DateTime? since = null, DateTime? before = null) =>
            SumCentsByAsync("DestinationAccountId", "DestinationAmountCents", since, before);

        // The column names are the constants above, never user input
        private async Task<List<AccountTotal>> SumCentsByAsync(string accountColumn, string amountColumn, DateTime? since,
            DateTime? before)
        {
            var (where, args) = DateFilter.Sql(since, before);
            return await (await ConnectionAsync()).QueryAsync<AccountTotal>(
                $"SELECT {accountColumn} AS AccountId, SUM(ABS({amountColumn})) AS Cents, MAX(Date) AS LastDateTicks " +
                $"FROM \"Transfer\" WHERE 1 = 1{where} GROUP BY {accountColumn}", args);
        }
    }
}
