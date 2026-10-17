using MoneyRecord.Helpers;
using SQLite;

namespace MoneyRecord.Models
{
    /// <summary>
    /// A trip: its expenses (<see cref="Transaction.TripId"/>) are real money out of an account, but sit in the "Travel"
    /// category (<see cref="Category.IsTravel"/>) while it is active; ending it moves each to a real category.
    /// One trip is active at a time (<see cref="EndedDate"/> null).
    /// </summary>
    public class Trip : IEntity
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public DateTime StartDate { get; set; } = DateTime.Today;

        /// <summary>Planned end; none = open-ended.</summary>
        public DateTime? EndDate { get; set; }

        /// <summary>The trip's currency (empty = base): its budget and summary are in it.</summary>
        public string CurrencyCode { get; set; } = string.Empty;

        /// <summary>Money value for view models; stored as <see cref="BudgetCents"/>. Null = no budget.</summary>
        [Ignore]
        public decimal? Budget
        {
            get => BudgetCents is { } cents ? Money.FromCents(cents) : null;
            set => BudgetCents = value is { } amount ? Money.ToCents(amount) : null;
        }

        public long? BudgetCents { get; set; }

        /// <summary>The account the add form preselects while the trip is active.</summary>
        public int? AccountId { get; set; }

        /// <summary>When the trip was ended; null = active.</summary>
        public DateTime? EndedDate { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        [Ignore]
        public bool IsActive => EndedDate == null;
    }

    /// <summary>A trip-only category (Lodging, Flights...), shared by every trip.</summary>
    public class TripCategory : IEntity
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string IconCode { get; set; } = "F0770";

        /// <summary>The real expense category it went to when a trip last ended; prefills the next end.</summary>
        public int? LastCategoryId { get; set; }

        [Ignore]
        public string DisplayIcon => IconHelper.GetCategoryDisplayIcon(IconCode);
    }

    /// <summary>A trip's spending limit for one trip category, in the trip's currency.</summary>
    public class TripBudget : IEntity
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed(Name = "IX_TripBudget_TripId")]
        public int TripId { get; set; }

        public int TripCategoryId { get; set; }

        public long LimitCents { get; set; }
    }

    /// <summary>A trip category a new database starts with (name = resource key).</summary>
    public sealed record DefaultTripCategory(string NameKey, string IconCode)
    {
        public static IReadOnlyList<DefaultTripCategory> All { get; } =
        [
            new(nameof(Resources.Strings.AppResources.TripCategoryLodging), "F02DE"),
            new(nameof(Resources.Strings.AppResources.TripCategoryFlights), "F001D"),
            new(nameof(Resources.Strings.AppResources.TripCategoryTransport), "F0BD8"),
            new(nameof(Resources.Strings.AppResources.TripCategoryFood), "F025A"),
            new(nameof(Resources.Strings.AppResources.TripCategoryActivities), "F0356"),
            new(nameof(Resources.Strings.AppResources.TripCategoryShopping), "F0110"),
            new(nameof(Resources.Strings.AppResources.TripCategoryOther), "F0076"),
        ];

        public TripCategory ToTripCategory() => new()
        {
            Name = Resources.Strings.AppResources.ResourceManager.GetString(NameKey, Resources.Strings.AppResources.Culture) ?? NameKey,
            IconCode = IconCode
        };
    }
}
