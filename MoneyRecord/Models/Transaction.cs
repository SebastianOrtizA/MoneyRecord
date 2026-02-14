using MoneyRecord.Helpers;
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
        /// Icon code for the category (Material Design Icons)
        /// </summary>
        [Ignore]
        public string CategoryIconCode { get; set; } = "F0770";

        /// <summary>
        /// Gets the displayable icon character from the category unicode code
        /// </summary>
        [Ignore]
        public string CategoryDisplayIcon => IconHelper.GetCategoryDisplayIcon(CategoryIconCode);

        /// <summary>
        /// Icon code for the account (Material Design Icons)
        /// </summary>
        [Ignore]
        public string AccountIconCode { get; set; } = "F0070";

        /// <summary>
        /// Gets the displayable icon character from the account unicode code
        /// </summary>
        [Ignore]
        public string AccountDisplayIcon => IconHelper.GetAccountDisplayIcon(AccountIconCode);

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
