using MoneyRecord.Helpers;
using SQLite;

namespace MoneyRecord.Models
{
    public class RecurringTransaction
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public decimal Amount { get; set; }

        public string Description { get; set; } = string.Empty;

        public int CategoryId { get; set; }

        public int AccountId { get; set; }

        public TransactionType TransactionType { get; set; }

        public RecurrenceFrequency Frequency { get; set; }

        public int? DayOfMonth { get; set; }

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
