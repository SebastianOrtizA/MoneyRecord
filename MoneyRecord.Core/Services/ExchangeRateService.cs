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

        /// <summary>How far back rates are always downloaded: the longest Net worth window and its baseline month.</summary>
        public const int HistoryMonths = 12;

        // Ranges before the stored days that came back empty (the source has nothing that old): not asked again while the
        // app runs. Only leading ranges: an empty trailing range may just be today's rate not published yet.
        private readonly HashSet<(string Base, string Quote, DateTime From, DateTime To)> _emptyLeadingRanges = [];

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

        public Task UpdateIfDueAsync() =>
            settings.LastUpdated?.Date == clock.GetLocalNow().Date ? Task.CompletedTask : UpdateIfEnabledAsync();

        public async Task UpdateIfEnabledAsync()
        {
            if (!settings.Enabled)
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
            // Net worth values balances at each month end of its window (up to 12 months + the month before): rates reach
            // back that far even without older entries
            var historyStart = new DateTime(today.Year, today.Month, 1).AddMonths(-HistoryMonths);

            var saved = 0;
            var unavailable = new List<string>();
            var failed = new List<string>();
            foreach (var quote in quotes)
            {
                var first = firstDates.TryGetValue(quote, out var date) && date.Date < historyStart ? date.Date : historyStart;
                try
                {
                    var stored = await rates.GetCoverageAsync(baseCode, quote);
                    foreach (var (from, to) in MissingRanges(stored, first, today))
                    {
                        var leading = stored is { } covered && to < covered.First;
                        if (leading && _emptyLeadingRanges.Contains((baseCode, quote, from, to)))
                            continue;

                        var downloaded = await source.GetRatesAsync(baseCode, quote, from, to, cancellationToken);
                        if (leading && downloaded.Count == 0)
                            _emptyLeadingRanges.Add((baseCode, quote, from, to));
                        saved += await rates.SaveRatesAsync(downloaded);
                    }
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

            if (saved > 0)
                _converter = null;
            // A currency the source doesn't offer won't get better by retrying; a failed download will
            if (failed.Count == 0)
                settings.LastUpdated = now;
            return new RateUpdateResult(saved, unavailable, failed);
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
