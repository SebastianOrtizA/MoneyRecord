using MoneyRecord.Models;

namespace MoneyRecord.Services.Interfaces
{
    /// <summary>Where daily rates come from (Frankfurter; tests use a fake).</summary>
    public interface IExchangeRateSource
    {
        /// <summary>
        /// The daily rates 1 <paramref name="baseCode"/> = x <paramref name="quoteCode"/> from <paramref name="from"/> to
        /// <paramref name="to"/> (days included).
        /// </summary>
        /// <exception cref="ExchangeRateUnavailableException">The source has no rates for this currency.</exception>
        /// <exception cref="HttpRequestException">No connection, or the source failed.</exception>
        Task<List<ExchangeRate>> GetRatesAsync(string baseCode, string quoteCode, DateTime from, DateTime to,
            CancellationToken cancellationToken = default);
    }

    /// <summary>The rate source doesn't offer this currency (trying again won't help).</summary>
    public sealed class ExchangeRateUnavailableException(string currencyCode)
        : Exception($"No exchange rates for {currencyCode}.")
    {
        public string CurrencyCode { get; } = currencyCode;
    }
}
