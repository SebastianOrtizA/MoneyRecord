namespace MoneyRecord.Models
{
    /// <summary>
    /// One row of a per-account SQL total: the summed amounts in cents and the latest date (as ticks, which is how
    /// sqlite-net stores dates). Mutable with a parameterless constructor because sqlite-net maps query rows onto it.
    /// </summary>
    public sealed class AccountTotal
    {
        public int AccountId { get; set; }

        public long Cents { get; set; }

        public long LastDateTicks { get; set; }

        public decimal Amount => Helpers.Money.FromCents(Cents);

        public DateTime LastDate => new(LastDateTicks);
    }
}
