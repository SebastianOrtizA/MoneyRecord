using CommunityToolkit.Mvvm.ComponentModel;
using MoneyRecord.Helpers;

namespace MoneyRecord.Models
{
    public enum GroupingMode
    {
        None,
        Category,
        Account,
        /// <summary>One group per day, newest first (or oldest first), with that day's net total per currency.</summary>
        Date
    }

    /// <summary>An expandable group of the Home list, built by <see cref="LedgerGrouping"/>.</summary>
    public partial class TransactionGroup : ObservableObject
    {
        [ObservableProperty]
        private bool isExpanded;

        /// <param name="totalsAreSigned">The totals carry their own sign (a day's net), instead of taking it from <paramref name="type"/>.</param>
        /// <param name="isExpanded">Starts open.</param>
        public TransactionGroup(string key, string groupName, string groupIconCode, TransactionType type,
            IReadOnlyList<CurrencyAmount> totals, IReadOnlyList<LedgerEntry> items, bool totalsAreSigned = false, bool isExpanded = false)
        {
            _totalsAreSigned = totalsAreSigned;
            this.isExpanded = isExpanded;
            Key = key;
            GroupName = groupName;
            GroupIconCode = groupIconCode;
            Type = type;
            Totals = totals;
            Items = items;
        }

        /// <summary>Stable across reloads ("d:2026-10-31", "c:7", "a:3"), so a group keeps its open / closed state.</summary>
        public string Key { get; }

        public string GroupName { get; }

        public string GroupIconCode { get; }

        public string GroupDisplayIcon => IconHelper.GetCategoryDisplayIcon(GroupIconCode);

        /// <summary>Income/Expense give the totals' sign (an account balance: its sign); Transfer shows them unsigned.</summary>
        public TransactionType Type { get; }

        /// <summary>The total in each currency of the group (base currency first): currencies are never added together.</summary>
        public IReadOnlyList<CurrencyAmount> Totals { get; }

        /// <summary>"-$35.00", "-US$5.00", one line each: each total with the group's sign (none for transfers).</summary>
        public string TotalText => CurrencyAmount.Format(_totalsAreSigned
            ? Totals
            : Totals.Select(t => t with
            {
                Amount = Type == TransactionType.Expense ? -Math.Abs(t.Amount) : Math.Abs(t.Amount)
            }).ToList(), separator: "\n");

        private readonly bool _totalsAreSigned;

        public IReadOnlyList<LedgerEntry> Items { get; }

        public int TransactionCount => Items.Count;

        public void ToggleExpanded()
        {
            IsExpanded = !IsExpanded;
        }
    }
}
