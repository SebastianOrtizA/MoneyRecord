using MoneyRecord.Resources.Strings;

namespace MoneyRecord.Models
{
    /// <summary>
    /// A category a new database starts with (and the first-run setup offers). The name is a resource key, so it is
    /// written in the language the app runs in when the category is created.
    /// </summary>
    public sealed record DefaultCategory(string NameKey, CategoryType Type, string IconCode)
    {
        public static IReadOnlyList<DefaultCategory> All { get; } =
        [
            new(nameof(AppResources.DefaultCategorySalary), CategoryType.Income, "F0116"),
            new(nameof(AppResources.DefaultCategoryFreelance), CategoryType.Income, "F00D6"),
            new(nameof(AppResources.DefaultCategoryInvestment), CategoryType.Income, "F081F"),
            new(nameof(AppResources.DefaultCategoryOtherIncome), CategoryType.Income, "F0CF4"),
            new(nameof(AppResources.DefaultCategoryFood), CategoryType.Expense, "F025A"),
            new(nameof(AppResources.DefaultCategoryTransportation), CategoryType.Expense, "F0BD8"),
            new(nameof(AppResources.DefaultCategoryEntertainment), CategoryType.Expense, "F0356"),
            new(nameof(AppResources.DefaultCategoryUtilities), CategoryType.Expense, "F0D15"),
            new(nameof(AppResources.DefaultCategoryShopping), CategoryType.Expense, "F0110"),
            new(nameof(AppResources.DefaultCategoryHome), CategoryType.Expense, "F0D15"),
            new(nameof(AppResources.DefaultCategoryOtherExpense), CategoryType.Expense, "F0076"),
        ];

        /// <summary>The name in the current app language.</summary>
        public string Name => AppResources.ResourceManager.GetString(NameKey, AppResources.Culture) ?? NameKey;

        public Category ToCategory() => new() { Name = Name, Type = Type, IconCode = IconCode };

        /// <summary>
        /// The user already has this category: same type and name (in the current language, ignoring case). The setup
        /// shows it locked and never inserts it twice.
        /// </summary>
        public bool Matches(Category category) =>
            category.Type == Type && string.Equals(category.Name, Name, StringComparison.CurrentCultureIgnoreCase);
    }
}
