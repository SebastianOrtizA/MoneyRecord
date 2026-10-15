using Microsoft.Extensions.Logging;
using MoneyRecord.Helpers;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Services
{
    public sealed class ExchangeRateService(
        IExchangeRateRepository rates,
        IAccountRepository accounts,
        ITransactionRepository transactions,
        IExchangeRateSource source,
        IExchangeRateSettings settings,
        TimeProvider clock,
        ILogger<ExchangeRateService> logger) : IExchangeRateService
    {
        // One update at a time (the app start and the daily job can overlap)
        private readonly SemaphoreSlim _updating = new(1, 1);
        private CurrencyConverter? _converter;

        public async Task<RateUpdateResult> UpdateAsync(CancellationToken cancellationToken = default)
        {
            await _updating.WaitAsync(cancellationToken);
            try
            {
                return await UpdateCoreAsync(cancellationToken);
            }
            finally
            {
                _updating.Release();
            }
        }

        public async Task UpdateIfDueAsync()
        {
            var today = clock.GetLocalNow().Date;
            if (!settings.Enabled || settings.LastUpdated?.Date == today)
                return;

            try
            {
                await UpdateAsync();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Exchange rate update failed");
            }
        }

        public async Task<CurrencyConverter> GetConverterAsync() =>
            _converter ??= new CurrencyConverter(await rates.GetAllAsync());

        private async Task<RateUpdateResult> UpdateCoreAsync(CancellationToken cancellationToken)
        {
            var now = clock.GetLocalNow().DateTime;
            var today = now.Date;
            var baseCode = MoneyFormatter.BaseCurrency;
            var quotes = ReportCurrencies.InUse(await accounts.GetAllAsync(), baseCode).Skip(1).ToList();
            var firstDates = await transactions.GetFirstDatesByCurrencyAsync(baseCode);

            var added = 0;
            var unavailable = new List<string>();
            var failed = new List<string>();
            foreach (var quote in quotes)
            {
                var first = firstDates.TryGetValue(quote, out var date) && date.Date < today ? date.Date : today;
                try
                {
                    foreach (var (from, to) in MissingRanges(await rates.GetCoverageAsync(baseCode, quote), first, today))
                        added += await rates.SaveRatesAsync(await source.GetRatesAsync(baseCode, quote, from, to, cancellationToken));
                }
                catch (ExchangeRateUnavailableException)
                {
                    unavailable.Add(quote);
                }
                catch (Exception ex) when ((ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
                                           && !cancellationToken.IsCancellationRequested)
                {
                    logger.LogInformation(ex, "No exchange rates downloaded for {Currency}", quote);
                    failed.Add(quote);
                }
            }

            if (added > 0)
                _converter = null;
            // A currency the source doesn't offer won't get better by retrying; a failed download will
            if (failed.Count == 0)
                settings.LastUpdated = now;
            return new RateUpdateResult(added, unavailable, failed);
        }

        /// <summary>
        /// The day ranges to download so the pair covers [<paramref name="first"/>, <paramref name="today"/>]: all of it
        /// when nothing is stored, else what lies before and after the stored days.
        /// </summary>
        public static List<(DateTime From, DateTime To)> MissingRanges((DateTime First, DateTime Last)? stored,
            DateTime first, DateTime today)
        {
            if (stored is not { } covered)
                return [(first, today)];

            var ranges = new List<(DateTime, DateTime)>();
            if (first < covered.First)
                ranges.Add((first, covered.First.AddDays(-1)));
            if (covered.Last < today)
                ranges.Add((covered.Last.AddDays(1), today));
            return ranges;
        }
    }
}
