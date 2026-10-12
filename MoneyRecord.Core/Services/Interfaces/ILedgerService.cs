using MoneyRecord.Models;

namespace MoneyRecord.Services.Interfaces
{
    /// <summary>Home's list: transactions and transfers as one set of <see cref="LedgerEntry"/> rows.</summary>
    public interface ILedgerService
    {
        /// <summary>
        /// The transactions and transfers dated in the range, unsorted; each transfer is one entry
        /// (<see cref="Helpers.LedgerTransfers.PerAccount"/> splits them for the account view).
        /// </summary>
        Task<List<LedgerEntry>> GetEntriesAsync(DateTime startDate, DateTime endDate);

        /// <summary>
        /// Deletes the entry's transaction (and its receipt photo) or its whole transfer, whichever half was chosen.
        /// An entry that is already gone is ignored.
        /// </summary>
        Task DeleteAsync(LedgerEntry entry);
    }
}
