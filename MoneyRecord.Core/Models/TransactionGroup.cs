using CommunityToolkit.Mvvm.ComponentModel;
using MoneyRecord.Helpers;

namespace MoneyRecord.Models
{
    public enum GroupingMode
    {
        None,
        Category,
        Account
    }

    /// <summary>An expandable group of the Home list, built by <see cref="LedgerGrouping"/>.</summary>
    public partial class TransactionGroup : ObservableObject
    {
        [ObservableProperty]
        private bool isExpanded;

        public TransactionGroup(string groupName, string groupIconCode, TransactionType type, decimal total,
            string currencyCode, IReadOnlyList<LedgerEntry> items)
        {
            GroupName = groupName;
            GroupIconCode = groupIconCode;
            Type = type;
            Total = total;
            CurrencyCode = currencyCode;
            Items = items;
        }

        public string GroupName { get; }

        public string GroupIconCode { get; }

        public string GroupDisplayIcon => IconHelper.GetCategoryDisplayIcon(GroupIconCode);

        /// <summary>Income/Expense give the total's sign (an account balance: its sign); Transfer shows it unsigned.</summary>
        public TransactionType Type { get; }

        public decimal Total { get; }

        /// <summary>The currency of <see cref="Total"/> (empty = the base currency).</summary>
        public string CurrencyCode { get; }

        public IReadOnlyList<LedgerEntry> Items { get; }

        public int TransactionCount => Items.Count;

        public void ToggleExpanded()
        {
            IsExpanded = !IsExpanded;
        }
    }
}
