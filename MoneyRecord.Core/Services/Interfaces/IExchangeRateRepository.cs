using MoneyRecord.Models;

namespace MoneyRecord.Services.Interfaces
{
    public interface IExchangeRateRepository : IRepository<ExchangeRate>
    {
        /// <summary>
        /// Stores downloaded rates in one transaction; a day already stored for the pair gets the new rate. Returns how many
        /// rows were saved (new or updated).
        /// </summary>
        Task<int> SaveRatesAsync(IReadOnlyCollection<ExchangeRate> rates);

        /// <summary>The first and last day stored for the pair; null when it has none.</summary>
        Task<(DateTime First, DateTime Last)?> GetCoverageAsync(string baseCode, string quoteCode);
    }
}
