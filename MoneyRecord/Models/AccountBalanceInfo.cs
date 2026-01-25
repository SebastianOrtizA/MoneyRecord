namespace MoneyRecord.Models
{
    public class AccountBalanceInfo
    {
        public int AccountId { get; set; }
        public string AccountName { get; set; } = string.Empty;
        public decimal CurrentBalance { get; set; }
        public DateTime? LastTransactionDate { get; set; }
        public string LastTransactionDateDisplay => LastTransactionDate.HasValue 
            ? LastTransactionDate.Value.ToString("MMM dd, yyyy") 
            : "No transactions";
    }
}
