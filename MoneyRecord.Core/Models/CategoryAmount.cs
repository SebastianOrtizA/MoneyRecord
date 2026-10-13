using MoneyRecord.Helpers;
using MoneyRecord.Resources.Strings;

namespace MoneyRecord.Models
{
    /// <summary>
    /// One category of the Spending or Income report: its total, its share of the report and its chart colour slot.
    /// The slot follows the category (its place among the type's categories by ID), never its rank, so a category
    /// keeps its colour when the period changes; categories past <see cref="PaletteSize"/> share the "Other" grey.
    /// </summary>
    public sealed record CategoryAmount(
        CategoryType Type,
        int CategoryId,
        string CategoryName,
        string IconCode,
        decimal Amount,
        double Percentage,
        int PaletteSlot)
    {
        /// <summary>How many categorical colours the charts have; more categories fold into "Other".</summary>
        public const int PaletteSize = 8;

        /// <summary>The slot of a category without its own colour (grey, shown as "Other" in the donut).</summary>
        public const int OtherSlot = -1;

        public bool IsIncome => Type == CategoryType.Income;

        public string DisplayIcon => IconHelper.GetCategoryDisplayIcon(IconCode);

        public string PercentageText => $"{Percentage:F1}%";

        /// <summary>The share as 0..1, for the bar under each list row.</summary>
        public double Share => Percentage / 100;
    }

    /// <summary>One donut slice: a category with its own colour, or every <see cref="CategoryAmount.OtherSlot"/> one merged.</summary>
    public sealed record ChartSlice(string Name, decimal Amount, int PaletteSlot);

    /// <summary>A report's total (in the base currency, see Stage 10.3) and its categories, largest share first.</summary>
    public sealed record CategoryReport(decimal Total, IReadOnlyList<CategoryAmount> Categories)
    {
        /// <summary>
        /// The donut's slices, largest first: each category with a colour slot, then one "Other" slice for the rest
        /// (none when every category has its own colour).
        /// </summary>
        public IReadOnlyList<ChartSlice> Slices
        {
            get
            {
                var slices = Categories
                    .Where(c => c.PaletteSlot != CategoryAmount.OtherSlot)
                    .Select(c => new ChartSlice(c.CategoryName, c.Amount, c.PaletteSlot))
                    .ToList();
                var other = Categories.Where(c => c.PaletteSlot == CategoryAmount.OtherSlot).Sum(c => c.Amount);
                if (other > 0)
                    slices.Add(new ChartSlice(AppResources.OtherCategories, other, CategoryAmount.OtherSlot));
                return slices;
            }
        }
    }
}
