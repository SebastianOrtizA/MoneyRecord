using MoneyRecord.Models;

namespace MoneyRecord.Services.Interfaces
{
    /// <summary>Home's list: transactions and transfers as one set of <see cref="LedgerEntry"/> rows.</summary>
    public interface ILedgerQueryService
    {
        /// <summary>
        /// The transactions and transfers dated in the range, unsorted. With <paramref name="transfersPerAccount"/> each
        /// transfer is two entries (out of the source, into the destination), otherwise one.
        /// </summary>
        Task<List<LedgerEntry>> GetEntriesAsync(DateTime startDate, DateTime endDate, bool transfersPerAccount);
    }
}
