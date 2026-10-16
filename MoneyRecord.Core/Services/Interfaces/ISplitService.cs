using MoneyRecord.Models;

namespace MoneyRecord.Services.Interfaces
{
    /// <summary>Saves a payment split across categories: one transaction per part, sharing a <see cref="Transaction.SplitId"/>.</summary>
    public interface ISplitService
    {
        /// <summary>
        /// Replaces <paramref name="replacedIds"/> (the transaction or split being edited; none when adding) with one transaction
        /// per part, in one database transaction. Every part copies <paramref name="template"/> (date, type, account, payee,
        /// tags, receipt) and gets its own category, amount and description; the template's own conversion ("Amount in USD")
        /// is shared out in proportion to the parts. One part = a plain transaction (no split); two or more keep the
        /// template's split ID, or get a new one.
        /// </summary>
        /// <returns>The saved transactions, in the parts' order.</returns>
        /// <exception cref="ArgumentException">No parts, or a part without a positive amount.</exception>
        Task<List<Transaction>> SaveAsync(Transaction template, IReadOnlyList<SplitPart> parts, IReadOnlyCollection<int> replacedIds);
    }
}
