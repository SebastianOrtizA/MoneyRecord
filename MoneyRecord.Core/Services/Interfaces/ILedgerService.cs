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
        /// Deletes the entry's transaction, every part of its split payment, or its whole transfer, whichever half was chosen,
        /// and returns what went so <see cref="RestoreAsync"/> can bring it back; null when the entry is already gone.
        /// The receipt photos stay on disk until <see cref="Discard"/>.
        /// </summary>
        Task<DeletedEntry?> DeleteAsync(LedgerEntry entry);

        /// <summary>Deletes a transfer by ID (the Transfers page); see <see cref="DeleteAsync"/>.</summary>
        Task<DeletedEntry?> DeleteTransferAsync(int transferId);

        /// <summary>
        /// Puts a deleted entry back with its original IDs, so what pointed at it (pocket take-outs, a party's own share) does again.
        /// False, and nothing written, when it can't be: a row with one of the IDs exists again or an account, category, payee,
        /// trip or debt it belongs to is gone.
        /// </summary>
        Task<bool> RestoreAsync(DeletedEntry deleted);

        /// <summary>The delete is final: removes the receipt photos kept for an undo.</summary>
        void Discard(DeletedEntry deleted);
    }
}
