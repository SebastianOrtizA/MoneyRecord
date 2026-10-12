using MoneyRecord.Helpers;
using SQLite;

namespace MoneyRecord.Models
{
    public class RecurringTransaction : IEntity
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        /// <summary>
        /// Money value for view models and JSON v1 backups; stored as <see cref="AmountCents"/>.
        /// </summary>
        [Ignore]
        public decimal Amount
        {
            get => Money.FromCents(AmountCents);
            set => AmountCents = Money.ToCents(value);
        }

        /// <summary>
        /// Stored value in cents (see <see cref="Money"/>). Declared after <see cref="Amount"/>, so a JSON backup writes it
        /// later in the file; on import System.Text.Json applies values in file order, so the exact value wins.
        /// </summary>
        public long AmountCents { get; set; }

        public string Description { get; set; } = string.Empty;

        public int CategoryId { get; set; }

        public int AccountId { get; set; }

        public TransactionType TransactionType { get; set; }

        public RecurrenceFrequency Frequency { get; set; }

        public int? DayOfMonth { get; set; }

        /// <summary>
        /// The day a monthly recurrence falls on: <see cref="DayOfMonth"/> when set, otherwise the start date's day,
        /// so one starting on the 31st goes Feb 28 → Mar 31 instead of staying on the 28th.
        /// </summary>
        [Ignore]
        public int? EffectiveDayOfMonth => Frequency == RecurrenceFrequency.Monthly ? DayOfMonth ?? StartDate.Day : DayOfMonth;

        public DateTime StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        public DateTime NextOccurrence { get; set; }

        public DateTime? LastProcessedDate { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        [Ignore]
        public string CategoryName { get; set; } = string.Empty;

        [Ignore]
        public string CategoryIconCode { get; set; } = "F0770";

        [Ignore]
        public string DisplayIcon => IconHelper.GetCategoryDisplayIcon(CategoryIconCode);

        [Ignore]
        public string AccountName { get; set; } = string.Empty;

        /// <summary>The account's currency (empty = the base currency), filled in when the transaction is loaded for display.</summary>
        [Ignore]
        public string CurrencyCode { get; set; } = string.Empty;

        [Ignore]
        public string FrequencyDisplay { get; set; } = string.Empty;
    }

    public enum RecurrenceFrequency
    {
        Daily,
        Weekly,
        Biweekly,
        Monthly,
        Yearly
    }

    public class RecurrenceFrequencyItem
    {
        public RecurrenceFrequency Frequency { get; set; }
        public string Display { get; set; } = string.Empty;

        public override string ToString() => Display;
    }
}
