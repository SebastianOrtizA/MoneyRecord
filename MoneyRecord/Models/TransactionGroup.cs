using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace MoneyRecord.Models
{
    public partial class TransactionGroup : ObservableObject
    {
        [ObservableProperty]
        private string categoryName = string.Empty;

        [ObservableProperty]
        private decimal total;

        [ObservableProperty]
        private int transactionCount;

        [ObservableProperty]
        private TransactionType type;

        [ObservableProperty]
        private bool isExpanded = false;

        [ObservableProperty]
        private ObservableCollection<Transaction> items = new();

        public TransactionGroup(string categoryName, List<Transaction> transactions)
        {
            CategoryName = categoryName ?? "Unknown";
            TransactionCount = transactions?.Count ?? 0;
            Items = new ObservableCollection<Transaction>(transactions ?? new List<Transaction>());
            
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

        public void ToggleExpanded()
        {
            IsExpanded = !IsExpanded;
        }
    }
}

