using MoneyRecord.Models;

namespace MoneyRecord.Services.Interfaces
{
    /// <summary>Safe to spend for the base currency (the first currency of the balance card) and its 30-day dip.</summary>
    public interface ISafeToSpendService
    {
        /// <summary>Null when there are no accounts.</summary>
        Task<HomeForecast?> GetAsync(CancellationToken cancellationToken = default);
    }
}
