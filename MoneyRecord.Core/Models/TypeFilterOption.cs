namespace MoneyRecord.Models
{
    /// <summary>An entry of the Home type filter: <see cref="Type"/> null = all types; <see cref="Name"/> is display only.</summary>
    public sealed record TypeFilterOption(TransactionType? Type, string Name);
}
