using SQLite;

namespace MoneyRecord.Models
{
    public class Transfer
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public DateTime Date { get; set; }

        public decimal Amount { get; set; }

        public int SourceAccountId { get; set; }

        public int DestinationAccountId { get; set; }

        public string Description { get; set; } = string.Empty;

        [Ignore]
        public string SourceAccountName { get; set; } = string.Empty;

        [Ignore]
        public string DestinationAccountName { get; set; } = string.Empty;
    }
}
