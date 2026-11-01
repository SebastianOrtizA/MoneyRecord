using MoneyRecord.Helpers;
using SQLite;

namespace MoneyRecord.Models
{
    /// <summary>
    /// A template for an expense or income the user repeats ("Coffee 3.50, Food, Cash"): one tap on its Home chip saves it
    /// as a transaction dated now. Never a transfer, a split or a trip expense.
    /// </summary>
    public class QuickEntry : IEntity
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        /// <summary>Expense or Income.</summary>
        public TransactionType Type { get; set; } = TransactionType.Expense;

        /// <summary>Money value for view models; stored as <see cref="AmountCents"/>.</summary>
        [Ignore]
        public decimal Amount
        {
            get => Money.FromCents(AmountCents);
            set => AmountCents = Money.ToCents(value);
        }

        /// <summary>Stored value in cents; 0 = ask the amount each time. Declared after <see cref="Amount"/> like the other models.</summary>
        public long AmountCents { get; set; }

        public int CategoryId { get; set; }

        public int AccountId { get; set; }

        public int? PayeeId { get; set; }

        /// <summary>The saved transaction's description; empty = the category's name.</summary>
        public string Description { get; set; } = string.Empty;

        public int SortOrder { get; set; }

        public int UseCount { get; set; }

        public DateTime? LastUsedDate { get; set; }

        [Ignore]
        public bool AsksAmount => AmountCents <= 0;
    }

    /// <summary>A Home chip: the quick entry and the text it shows.</summary>
    public sealed record QuickEntryChip(QuickEntryItem Item, string Text);

    /// <summary>A quick entry with the names and currency a chip or the Quick entries page shows.</summary>
    public sealed record QuickEntryItem(QuickEntry Entry, string CategoryName, string CategoryIconCode, string AccountName,
        string CurrencyCode)
    {
        public int Id => Entry.Id;

        public string Name => Entry.Name;

        public bool AsksAmount => Entry.AsksAmount;

        public string CategoryDisplayIcon => IconHelper.GetCategoryDisplayIcon(CategoryIconCode);

        /// <summary>"3.50" in the account's currency; empty when the amount is asked each time.</summary>
        public string AmountText => AsksAmount ? string.Empty : MoneyFormatter.Format(Entry.Amount, CurrencyCode);

        /// <summary>The chip's text: "Coffee 3.50", or just the name when the amount is asked.</summary>
        public string ChipText => AsksAmount ? Name : $"{Name} {AmountText}";

        /// <summary>Its category or account is gone (a deleted one removes the quick entry, so only a damaged database gets here).</summary>
        public bool IsBroken => CategoryName.Length == 0 || AccountName.Length == 0;
    }
}
