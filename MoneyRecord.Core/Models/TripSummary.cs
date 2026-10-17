namespace MoneyRecord.Models
{
    /// <summary>What was spent in one trip category, with its limit if the trip set one (trip currency).</summary>
    public sealed record TripCategorySpent(TripCategory Category, decimal Spent, decimal? Limit)
    {
        public double Fraction => Limit is > 0 ? (double)(Spent / Limit.Value) : 0;

        public BudgetLevel Level => Limit is null ? BudgetLevel.OnTrack : LevelOf(Fraction);

        public static BudgetLevel LevelOf(double fraction) => fraction switch
        {
            >= 1 => BudgetLevel.Exceeded,
            >= 0.8 => BudgetLevel.High,
            >= 0.6 => BudgetLevel.Warning,
            _ => BudgetLevel.OnTrack
        };
    }

    /// <summary>
    /// A trip's spending in its own currency: converted with the downloaded rates; what has none stays in its own
    /// currency in <see cref="NotConverted"/> (never added).
    /// </summary>
    public sealed record TripSummary(Trip Trip, string CurrencyCode, decimal Spent, int EntryCount,
        IReadOnlyList<TripCategorySpent> Categories, IReadOnlyList<CurrencyAmount> NotConverted)
    {
        public decimal? Budget => Trip.Budget;

        public double Fraction => Budget is > 0 ? (double)(Spent / Budget.Value) : 0;

        public BudgetLevel Level => Budget is null ? BudgetLevel.OnTrack : TripCategorySpent.LevelOf(Fraction);
    }
}
