using MoneyRecord.Helpers;
using MoneyRecord.Resources.Strings;

namespace MoneyRecord.Models
{
    /// <summary>A trip as the Trips page and Home show it: dates, spent of the budget, status.</summary>
    public sealed class TripCard(TripSummary summary)
    {
        public TripSummary Summary { get; } = summary;

        public Trip Trip => Summary.Trip;

        public int TripId => Trip.Id;

        public string Name => Trip.Name;

        public bool IsActive => Trip.IsActive;

        public string StateText => IsActive ? AppResources.TripActive : AppResources.TripEnded;

        public string DatesText => Trip.EndDate is { } end
            ? DateFormatter.FormatRange(Trip.StartDate, end)
            : string.Format(AppResources.TripSince, DateFormatter.Format(Trip.StartDate, DateStyle.Medium));

        public bool HasBudget => Summary.Budget is > 0;

        public string SpentText => Summary.Budget is { } budget
            ? string.Format(AppResources.SpentOfBudget, MoneyFormatter.Format(Summary.Spent, Summary.CurrencyCode), MoneyFormatter.Format(budget, Summary.CurrencyCode))
            : string.Format(AppResources.TripSpent, MoneyFormatter.Format(Summary.Spent, Summary.CurrencyCode));

        /// <summary>For a progress bar (0–100).</summary>
        public double ProgressPercentage => Math.Min(100, Summary.Fraction * 100);

        public BudgetLevel Level => Summary.Level;

        public string StatusText => Level switch
        {
            BudgetLevel.Exceeded => AppResources.OverBudget,
            BudgetLevel.Warning or BudgetLevel.High => AppResources.NearLimit,
            _ => AppResources.OnTrack
        };

        /// <summary>What could not be converted into the trip currency ("Not converted: $50.00"); empty when everything was.</summary>
        public string NotConvertedText => Summary.NotConverted.Count == 0
            ? string.Empty
            : string.Format(AppResources.TripNotConverted,
                string.Join(", ", Summary.NotConverted.Select(a => MoneyFormatter.FormatWithCode(a.Amount, a.CurrencyCode))));

        public bool HasNotConverted => Summary.NotConverted.Count > 0;
    }
}

namespace MoneyRecord.Models
{
    /// <summary>A trip category's line in the trip page: spent (of its limit).</summary>
    public sealed class TripCategoryRow(TripCategorySpent spent, string currencyCode)
    {
        public string Name => spent.Category.Name;

        public string DisplayIcon => spent.Category.DisplayIcon;

        public bool HasLimit => spent.Limit is > 0;

        public string SpentText => spent.Limit is { } limit
            ? string.Format(AppResources.SpentOfBudget, MoneyFormatter.Format(spent.Spent, currencyCode), MoneyFormatter.Format(limit, currencyCode))
            : MoneyFormatter.Format(spent.Spent, currencyCode);

        public double ProgressPercentage => Math.Min(100, spent.Fraction * 100);

        public BudgetLevel Level => spent.Level;
    }

    /// <summary>One expense of a trip in the trip page.</summary>
    public sealed record TripEntryRow(string Title, string Details, string AmountText);
}
