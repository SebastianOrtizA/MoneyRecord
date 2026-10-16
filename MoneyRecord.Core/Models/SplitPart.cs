namespace MoneyRecord.Models
{
    /// <summary>One category's share of a split payment: saved as its own transaction (see <see cref="Transaction.SplitId"/>).</summary>
    /// <param name="Description">The part's description, already resolved (empty → its category's name).</param>
    public sealed record SplitPart(int CategoryId, long AmountCents, string Description);
}
