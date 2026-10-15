using MoneyRecord.Helpers;
using MoneyRecord.Resources.Strings;

namespace MoneyRecord.Models
{
    /// <summary>One scheduled occurrence of a recurring transaction that hasn't been created yet (the Upcoming lists).</summary>
    public sealed class UpcomingEntry
    {
        public int RecurringId { get; init; }

        /// <summary>The occurrence's day (00:00).</summary>
        public DateTime Date { get; init; }

        /// <summary>Days from today: 0 = today, 1 = tomorrow.</summary>
        public int DaysAway { get; init; }

        public string Description { get; init; } = string.Empty;

        public decimal Amount { get; init; }

        public TransactionType Type { get; init; }

        public string CategoryName { get; init; } = string.Empty;

        public string CategoryIconCode { get; init; } = "F0770";

        public string AccountName { get; init; } = string.Empty;

        /// <summary>The account's currency (empty = the base currency).</summary>
        public string CurrencyCode { get; init; } = string.Empty;

        public string DisplayIcon => IconHelper.GetCategoryDisplayIcon(CategoryIconCode);

        /// <summary>Negative for expenses, like the ledger rows.</summary>
        public decimal SignedAmount => Type == TransactionType.Expense ? -Amount : Amount;

        /// <summary>"Today", "Tomorrow", "In 3 days".</summary>
        public string WhenText => DaysAway switch
        {
            0 => AppResources.Today,
            1 => AppResources.Tomorrow,
            _ => Plurals.InDays(DaysAway)
        };
    }
}
