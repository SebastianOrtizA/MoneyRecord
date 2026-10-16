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

        public Task<List<AccountTotal>> SumCentsBySourceAsync(DateTime after = default) =>
            SumCentsByAsync("SourceAccountId", "AmountCents", after);

        // The destination receives its own amount (another currency's, for a cross-currency transfer)
        public Task<List<AccountTotal>> SumCentsByDestinationAsync(DateTime after = default) =>
            SumCentsByAsync("DestinationAccountId", "DestinationAmountCents", after);

        // The column names are the constants above, never user input
        private async Task<List<AccountTotal>> SumCentsByAsync(string accountColumn, string amountColumn, DateTime after) =>
            await (await ConnectionAsync()).QueryAsync<AccountTotal>(
                $"SELECT {accountColumn} AS AccountId, SUM(ABS({amountColumn})) AS Cents, MAX(Date) AS LastDateTicks " +
                $"FROM \"Transfer\" WHERE Date > ? GROUP BY {accountColumn}", after);
    }
}
