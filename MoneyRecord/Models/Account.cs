using SQLite;

namespace MoneyRecord.Models
{
    public class Account
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public decimal InitialBalance { get; set; }

        public bool IsDefault { get; set; }
    }
}
