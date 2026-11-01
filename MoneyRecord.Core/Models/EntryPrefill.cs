namespace MoneyRecord.Models
{
    /// <summary>
    /// What a new entry can start from when it repeats an earlier one: a duplicated transaction, or a payee's latest entry.
    /// Only the repeatable fields: a receipt photo, a split, a trip and a recurrence belong to the original and are never copied.
    /// </summary>
    public sealed record EntryPrefill(long AmountCents, int CategoryId, int? AccountId, int? PayeeId, string? Tags, string Description)
    {
        public static EntryPrefill From(Transaction transaction) => new(
            transaction.AmountCents, transaction.CategoryId, transaction.AccountId, transaction.PayeeId,
            transaction.Tags, transaction.Description);
    }
}
