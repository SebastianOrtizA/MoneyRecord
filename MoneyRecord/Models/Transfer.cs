using MoneyRecord.Helpers;
using SQLite;

namespace MoneyRecord.Models
{
    public class Transfer
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed(Name = "IX_Transfer_Date")]
        public DateTime Date { get; set; }

        /// <summary>
        /// Money value for view models and JSON v1 backups; stored as <see cref="AmountCents"/>.
        /// </summary>
        [Ignore]
        public decimal Amount
        {
            get => Money.FromCents(AmountCents);
            set => AmountCents = Money.ToCents(value);
        }

        /// <summary>
        /// Stored value in cents (see <see cref="Money"/>). Declared after <see cref="Amount"/>, so a JSON backup writes it
        /// later in the file; on import System.Text.Json applies values in file order, so the exact value wins.
        /// </summary>
        public long AmountCents { get; set; }

        [Indexed(Name = "IX_Transfer_SourceAccountId")]
        public int SourceAccountId { get; set; }

        [Indexed(Name = "IX_Transfer_DestinationAccountId")]
        public int DestinationAccountId { get; set; }

        public string Description { get; set; } = string.Empty;

        [Ignore]
        public string SourceAccountName { get; set; } = string.Empty;

        [Ignore]
        public string DestinationAccountName { get; set; } = string.Empty;
    }
}
