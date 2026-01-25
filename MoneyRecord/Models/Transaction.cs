using SQLite;

namespace MoneyRecord.Models
{
    public class Transaction
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public DateTime Date { get; set; }

        public string Description { get; set; } = string.Empty;

        public decimal Amount { get; set; }

        public int CategoryId { get; set; }

        public TransactionType Type { get; set; }

        public int? AccountId { get; set; }

        [Ignore]
        public string CategoryName { get; set; } = string.Empty;

        [Ignore]
        public string AccountName { get; set; } = string.Empty;

        /// <summary>
        /// Used for transfers to store the original Transfer.Id for edit/delete operations
        /// </summary>
        [Ignore]
        public int? TransferId { get; set; }

        /// <summary>
        /// Indicates if this is an outgoing transfer (negative) or incoming transfer (positive)
        /// </summary>
        [Ignore]
        public bool IsOutgoingTransfer { get; set; }

        /// <summary>
        /// Display name for transfer counterpart account
        /// </summary>
        [Ignore]
        public string TransferCounterpartAccount { get; set; } = string.Empty;
    }

    public enum TransactionType
    {
        Income,
        Expense,
        Transfer
    }
}
