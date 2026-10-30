namespace MoneyRecord.Models
{
    /// <summary>
    /// What a delete removed (the transaction, every part of a split, or the transfer), as stored rows with their IDs, so an undo
    /// can put them back exactly. The receipt photos are still on disk until <c>ILedgerService.Discard</c>.
    /// </summary>
    public sealed record DeletedEntry(IReadOnlyList<Transaction> Transactions, Transfer? Transfer)
    {
        public IReadOnlyList<string> ReceiptPaths { get; } = Transactions
            .Select(t => t.ReceiptPhotoPath)
            .Where(p => !string.IsNullOrEmpty(p))
            .Select(p => p!)
            .Distinct()
            .ToList();
    }
}
