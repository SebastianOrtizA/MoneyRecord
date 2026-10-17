namespace MoneyRecord.Models
{
    public class AccountBalanceInfo
    {
        public int AccountId { get; set; }
        public string AccountName { get; set; } = string.Empty;
        public decimal CurrentBalance { get; set; }
        public string CurrencyCode { get; set; } = string.Empty;
        /// <summary>A debt's hidden account (<see cref="Account.IsDebt"/>), not one of the user's own.</summary>
        public bool IsDebt { get; set; }
        /// <summary>
        /// The most recent activity date for the account.
        /// This is the latest of: last transaction date, last transfer date, or account created date.
        /// </summary>
        public DateTime? LastActivityDate { get; set; }
    }
}
