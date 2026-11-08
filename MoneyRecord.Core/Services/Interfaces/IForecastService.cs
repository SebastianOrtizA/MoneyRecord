using MoneyRecord.Models;

namespace MoneyRecord.Services.Interfaces
{
    /// <summary>
    /// Where the balances are heading: today's balances plus the recurring occurrences and future-dated entries still to
    /// come. An estimate, not a promise.
    /// </summary>
    public interface IForecastService
    {
        /// <summary>The forecast for <paramref name="days"/> days (30, 60 or 90) from today.</summary>
        Task<ForecastResult> GetForecastAsync(int days, CancellationToken cancellationToken = default);
    }
}
