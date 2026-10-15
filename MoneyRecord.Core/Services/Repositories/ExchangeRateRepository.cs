using MoneyRecord.Models;

namespace MoneyRecord.Services.Repositories
{
    public sealed class ExchangeRateRepository(DatabaseInitializer database)
        : RepositoryBase<ExchangeRate>(database), Interfaces.IExchangeRateRepository
    {
        public async Task<int> SaveRatesAsync(IReadOnlyCollection<ExchangeRate> rates)
        {
            if (rates.Count == 0)
                return 0;

            // An upsert on the (base, quote, date) index; sqlite-net's "OR REPLACE" would write the AutoIncrement ID 0
            var db = await ConnectionAsync();
            await db.RunInTransactionAsync(connection =>
            {
                foreach (var rate in rates)
                {
                    connection.Execute(
                        "INSERT INTO \"ExchangeRate\" (Date, BaseCode, QuoteCode, Rate) VALUES (?, ?, ?, ?) " +
                        "ON CONFLICT(BaseCode, QuoteCode, Date) DO UPDATE SET Rate = excluded.Rate",
                        rate.Date.Date.Ticks, rate.BaseCode.ToUpperInvariant(), rate.QuoteCode.ToUpperInvariant(), rate.Rate);
                }
            });
            OnChanged();
            return rates.Count;
        }

        public async Task<(DateTime First, DateTime Last)?> GetCoverageAsync(string baseCode, string quoteCode)
        {
            var rows = await (await ConnectionAsync()).QueryAsync<Coverage>(
                "SELECT MIN(Date) AS FirstTicks, MAX(Date) AS LastTicks FROM \"ExchangeRate\" WHERE BaseCode = ? AND QuoteCode = ?",
                baseCode.ToUpperInvariant(), quoteCode.ToUpperInvariant());
            return rows.FirstOrDefault() is { FirstTicks: > 0 } coverage
                ? (new DateTime(coverage.FirstTicks), new DateTime(coverage.LastTicks))
                : null;
        }

        private sealed class Coverage
        {
            public long FirstTicks { get; set; }

            public long LastTicks { get; set; }
        }
    }
}
