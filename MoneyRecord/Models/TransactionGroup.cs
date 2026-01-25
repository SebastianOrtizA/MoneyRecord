using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace MoneyRecord.Models
{
    public enum GroupingMode
    {
        None,
        Category,
        Account
    }

    public partial class TransactionGroup : ObservableObject
    {
        [ObservableProperty]
        private string groupName = string.Empty;

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

        [ObservableProperty]
        private bool isBalanceMode = false;

        public TransactionGroup(string groupName, List<Transaction> transactions, GroupingMode mode = GroupingMode.Category, decimal? overrideTotal = null)
        {
            GroupName = groupName ?? "Unknown";
            CategoryName = groupName ?? "Unknown"; // Keep for backward compatibility
            TransactionCount = transactions?.Count ?? 0;
            Items = new ObservableCollection<Transaction>(transactions ?? new List<Transaction>());
            IsBalanceMode = mode == GroupingMode.Account;
            
            if (overrideTotal.HasValue)
            {
                // For account grouping, use the provided balance
                Total = overrideTotal.Value;
                // Determine type based on whether balance is positive or negative
                Type = Total >= 0 ? TransactionType.Income : TransactionType.Expense;
            }
            else if (transactions != null && transactions.Any())
            {
                // For category grouping, sum the transaction amounts
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

