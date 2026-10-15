using MoneyRecord.Helpers;

namespace MoneyRecord.Services.Interfaces
{
    /// <summary>The outcome of a rate update: how many daily rates were stored, and the currencies that got none.</summary>
    public sealed record RateUpdateResult(int Added, IReadOnlyList<string> Unavailable, IReadOnlyList<string> Failed)
    {
        /// <summary>Every currency in use has rates up to today.</summary>
        public bool Complete => Unavailable.Count == 0 && Failed.Count == 0;
    }

    /// <summary>Keeps the downloaded daily rates up to date and converts with them.</summary>
    public interface IExchangeRateService
    {
        /// <summary>
        /// Downloads what is missing: for every account currency other than the base, from its first transaction (or
        /// today) to today, around the days already stored.
        /// </summary>
        Task<RateUpdateResult> UpdateAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// <see cref="UpdateAsync"/> when the download is on and hasn't completed today; best-effort (failures are logged).
        /// For the app start and the daily job.
        /// </summary>
        Task UpdateIfDueAsync();

        /// <summary>A converter over every stored rate (rebuilt after an update).</summary>
        Task<CurrencyConverter> GetConverterAsync();
    }
}
