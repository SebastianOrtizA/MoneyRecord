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

        /// <summary>Days from today: 0 = today (or overdue, still to be created), 1 = tomorrow.</summary>
        public int DaysAway { get; init; }

        public string Description { get; init; } = string.Empty;

        public decimal Amount { get; init; }

        public TransactionType Type { get; init; }

        public string CategoryName { get; init; } = string.Empty;

        public string CategoryIconCode { get; init; } = IconHelper.DefaultCategoryIconCode;

        public bool IsVariable { get; init; }

        /// <summary>A variable-amount occurrence that is due (today or overdue): it waits for the user to enter or skip it.</summary>
        public bool IsPending { get; init; }

        /// <summary>A variable occurrence with a last amount to show as the estimate (<see cref="Amount"/>).</summary>
        public bool HasEstimate => IsVariable && Amount != 0;

        /// <summary>A variable occurrence's estimate: "≈ $85" from the last amount, or "Amount varies".</summary>
        public string EstimateText => !IsVariable ? string.Empty
            : HasEstimate ? "≈ " + MoneyFormatter.Format(Amount, CurrencyCode) : AppResources.AmountVaries;

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
