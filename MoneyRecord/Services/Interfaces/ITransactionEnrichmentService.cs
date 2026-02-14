using MoneyRecord.Models;

namespace MoneyRecord.Services.Interfaces
{
    /// <summary>
    /// Service interface for enriching transactions with related entity data.
    /// Single responsibility: Populating navigation properties on transactions.
    /// </summary>
    public interface ITransactionEnrichmentService
    {
        Task<List<Transaction>> GetEnrichedTransactionsAsync(DateTime startDate, DateTime endDate);
        Task<List<Transfer>> GetEnrichedTransfersAsync(DateTime startDate, DateTime endDate);
        Task<List<Transfer>> GetAllEnrichedTransfersAsync();
        Task<Transfer?> GetEnrichedTransferAsync(int id);
    }
}
