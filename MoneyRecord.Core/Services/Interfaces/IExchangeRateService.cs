using MoneyRecord.Helpers;

namespace MoneyRecord.Services.Interfaces
{
    /// <summary>
    /// The outcome of a rate update: how many daily rates were saved (new or updated), the currencies the source doesn't
    /// offer, and those whose download failed.
    /// </summary>
    public sealed record RateUpdateResult(int Saved, IReadOnlyList<string> Unavailable, IReadOnlyList<string> Failed);

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

        /// <summary>
        /// <see cref="UpdateAsync"/> when the download is on, whenever it last ran; best-effort. After saving an account or a
        /// transaction: a new currency or an older date gets its rates now, not tomorrow (only missing days are fetched, so
        /// it usually sends nothing).
        /// </summary>
        Task UpdateIfEnabledAsync();

        /// <summary>A converter over every stored rate (rebuilt after an update).</summary>
        Task<CurrencyConverter> GetConverterAsync();
    }
}
