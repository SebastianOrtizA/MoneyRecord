using MoneyRecord.Helpers;

namespace MoneyRecord.Models
{
    /// <summary>
    /// One category of the Expense or Income report: its total, its share of the report and its heatmap level
    /// (0 = the smallest share … <see cref="HeatLevels"/> − 1 = the largest), which the app maps to a colour.
    /// </summary>
    public sealed record CategoryAmount(
        CategoryType Type,
        int CategoryId,
        string CategoryName,
        string IconCode,
        decimal Amount,
        double Percentage,
        int HeatLevel)
    {
        public const int HeatLevels = 12;

        /// <summary>Categories under this share get the "&lt;10%" badge.</summary>
        public const double MinorShareThreshold = 10.0;

        public bool IsMinorShare => Percentage < MinorShareThreshold;

        public bool IsIncome => Type == CategoryType.Income;

        public string DisplayIcon => IconHelper.GetCategoryDisplayIcon(IconCode);

        public string PercentageText => $"{Percentage:F1}%";
    }

    /// <summary>A report's total (in the base currency, see Stage 10.3) and its categories, largest share first.</summary>
    public sealed record CategoryReport(decimal Total, IReadOnlyList<CategoryAmount> Categories);
}
