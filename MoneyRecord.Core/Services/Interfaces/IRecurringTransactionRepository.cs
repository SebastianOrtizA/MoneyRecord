using MoneyRecord.Models;

namespace MoneyRecord.Services.Interfaces
{
    public interface IRecurringTransactionRepository : IRepository<RecurringTransaction>
    {
        Task<List<RecurringTransaction>> GetActiveAsync();
        Task<List<RecurringTransaction>> GetDueAsync(DateTime asOf);
        Task<int> DeleteByIdAsync(int id);
        Task<int> DeactivateAsync(int id);
    }
}
