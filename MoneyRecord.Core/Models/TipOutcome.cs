namespace MoneyRecord.Models
{
    /// <summary>How a tip ended.</summary>
    public enum TipOutcome
    {
        /// <summary>Paid and consumed: the same tip can be given again.</summary>
        Thanked,

        /// <summary>The user closed the store's sheet without paying.</summary>
        Cancelled,

        /// <summary>Waiting for the payment (cash at a shop, a slow bank): it is settled by a later start.</summary>
        Pending,

        Failed
    }
}
