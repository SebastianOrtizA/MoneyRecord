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

        [ObservableProperty]
        private string groupIconCode = "F0770"; // Default tag icon

        /// <summary>
        /// Gets the displayable icon character from the unicode code
        /// </summary>
        public string GroupDisplayIcon
        {
            get
            {
                try
                {
                    if (string.IsNullOrEmpty(GroupIconCode))
                        return "\uF0770";

                    var codePoint = Convert.ToInt32(GroupIconCode, 16);
                    return char.ConvertFromUtf32(codePoint);
                }
                catch
                {
                    return "\uF0770";
                }
            }
        }

        public TransactionGroup(string groupName, List<Transaction> transactions, GroupingMode mode = GroupingMode.Category, decimal? overrideTotal = null, string? accountIconCode = null)
        {
            GroupName = groupName ?? "Unknown";
            CategoryName = groupName ?? "Unknown"; // Keep for backward compatibility
            TransactionCount = transactions?.Count ?? 0;
            Items = new ObservableCollection<Transaction>(transactions ?? new List<Transaction>());
            IsBalanceMode = mode == GroupingMode.Account;

            // Set group icon based on grouping mode
            if (mode == GroupingMode.Category && transactions != null && transactions.Any())
            {
                // For category grouping, use the first transaction's category icon
                GroupIconCode = transactions.First().CategoryIconCode ?? "F0770";
            }
            else if (mode == GroupingMode.Account)
            {
                // For account grouping, prefer the explicitly passed account icon
                // Otherwise, find a non-transfer transaction to get the real account icon
                if (!string.IsNullOrEmpty(accountIconCode))
                {
                    GroupIconCode = accountIconCode;
                }
                else if (transactions != null && transactions.Any())
                {
                    // Try to find a non-transfer transaction to get the real account icon
                    var nonTransferTransaction = transactions.FirstOrDefault(t => t.Type != TransactionType.Transfer);
                    GroupIconCode = nonTransferTransaction?.AccountIconCode 
                        ?? transactions.First().AccountIconCode 
                        ?? "F0070";
                }
                else
                {
                    GroupIconCode = "F0070"; // Default bank icon
                }
            }
            else
            {
                GroupIconCode = mode == GroupingMode.Account ? "F0070" : "F0770";
            }
            
            if (overrideTotal.HasValue)
            {
                // For account grouping, use the provided balance
                Total = overrideTotal.Value;
                // Determine type based on whether balance is positive or negative
                Type = Total >= 0 ? TransactionType.Income : TransactionType.Expense;
            }
            else if (transactions != null && transactions.Any())
            {
                // Check if this is a transfer-only group
                var hasOnlyTransfers = transactions.All(t => t.Type == TransactionType.Transfer);
                
                if (hasOnlyTransfers)
                {
                    // For transfer category group, sum all transfer amounts (neutral display)
                    Type = TransactionType.Transfer;
                    Total = transactions.Sum(t => t.Amount);
                }
                else
                {
                    // For regular category grouping, sum the transaction amounts
                    // Exclude transfers from the sum if mixed (shouldn't happen normally)
                    var nonTransferTransactions = transactions.Where(t => t.Type != TransactionType.Transfer).ToList();
                    Type = nonTransferTransactions.FirstOrDefault()?.Type ?? TransactionType.Expense;
                    Total = nonTransferTransactions.Sum(t => t.Amount);
                }
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

