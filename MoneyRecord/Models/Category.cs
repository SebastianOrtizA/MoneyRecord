using SQLite;

namespace MoneyRecord.Models
{
    public class Category
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public CategoryType Type { get; set; }
    }

    public enum CategoryType
    {
        Income,
        Expense
    }
}
