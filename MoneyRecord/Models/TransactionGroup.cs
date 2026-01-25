using System.Collections.ObjectModel;

namespace MoneyRecord.Models
{
    public class TransactionGroup : ObservableCollection<Transaction>
    {
        public string CategoryName { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public int TransactionCount { get; set; }
        public TransactionType Type { get; set; }

        public TransactionGroup(string categoryName, List<Transaction> transactions) : base(transactions ?? new List<Transaction>())
        {
            CategoryName = categoryName ?? "Unknown";
            TransactionCount = transactions?.Count ?? 0;
            
            if (transactions != null && transactions.Any())
            {
                Type = transactions.FirstOrDefault()?.Type ?? TransactionType.Expense;
                Total = transactions.Sum(t => t.Amount);
            }
            else
            {
                Type = TransactionType.Expense;
                Total = 0;
            }
        }
    }
}

