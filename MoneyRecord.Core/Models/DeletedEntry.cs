namespace MoneyRecord.Models
{
    /// <summary>
    /// What a delete removed (the transaction, every part of a split, or the transfer), as stored rows with their IDs, so an undo
    /// can put them back exactly. The attached files are still on disk until <c>ILedgerService.Discard</c>.
    /// </summary>
    public sealed record DeletedEntry(IReadOnlyList<Transaction> Transactions, Transfer? Transfer,
        IReadOnlyList<TransactionAttachment>? Attachments = null)
    {
        public IReadOnlyList<string> ReceiptPaths { get; } = Transactions
            .Select(t => t.ReceiptPhotoPath)
            .Concat((Attachments ?? []).Select(a => a.Path))
            .Where(p => !string.IsNullOrEmpty(p))
            .Select(p => p!)
            .Distinct()
            .ToList();
    }
}
