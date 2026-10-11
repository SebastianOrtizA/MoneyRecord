using SQLite;

namespace MoneyRecord.Models
{
    public class Transfer
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed(Name = "IX_Transfer_Date")]
        public DateTime Date { get; set; }

        public decimal Amount { get; set; }

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
