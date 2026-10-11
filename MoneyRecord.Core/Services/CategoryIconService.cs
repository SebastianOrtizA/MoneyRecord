using MoneyRecord.Models;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Services
{
    /// <summary>
    /// Service providing Material Design Icons for categories and accounts
    /// Icons from: https://materialdesignicons.com (MIT/Apache 2.0 License)
    /// </summary>
    public sealed class CategoryIconService : ICategoryIconService
    {
        /// <summary>
        /// Gets available icons for expense categories
        /// </summary>
        public List<CategoryIcon> GetExpenseIcons()
        {
            return
            [
                new CategoryIcon { Code = "F025A", Name = "Food" },             // food
                new CategoryIcon { Code = "F0BD8", Name = "Train/Car" },        // train-car
                new CategoryIcon { Code = "F0356", Name = "Cocktail" },         // glass-cocktail
                new CategoryIcon { Code = "F0076", Name = "Basket" },           // basket
                new CategoryIcon { Code = "F0110", Name = "Cart" },             // cart
                new CategoryIcon { Code = "F0D15", Name = "Home" },             // home-city
                new CategoryIcon { Code = "F03E9", Name = "Pet" },              // paw
                new CategoryIcon { Code = "F02E1", Name = "Medical" },          // hospital-building
            ];
        }

        /// <summary>
        /// Gets available icons for income categories
        /// </summary>
        public List<CategoryIcon> GetIncomeIcons()
        {
            return
            [
                new CategoryIcon { Code = "F0116", Name = "Cash" },             // cash-multiple
                new CategoryIcon { Code = "F081F", Name = "Finance" },          // finance
                new CategoryIcon { Code = "F00D6", Name = "Briefcase" },        // briefcase
                new CategoryIcon { Code = "F0CF4", Name = "Register" },         // cash-register
            ];
        }

        /// <summary>
        /// Gets icons based on category type
        /// </summary>
        public List<CategoryIcon> GetIconsForType(CategoryType type)
        {
            return type == CategoryType.Income ? GetIncomeIcons() : GetExpenseIcons();
        }

        /// <summary>
        /// Gets the default icon code for a category type
        /// </summary>
        public string GetDefaultIconCode(CategoryType type)
        {
            return type == CategoryType.Income ? "F0116" : "F09B5"; // cash-multiple or food
        }

        /// <summary>
        /// Gets available icons for accounts
        /// </summary>
        public List<AccountIcon> GetAccountIcons()
        {
            return
            [
                new AccountIcon { Code = "F0070", Name = "Bank" },              // bank
                new AccountIcon { Code = "F1007", Name = "Piggy Bank" },        // piggy-bank
                new AccountIcon { Code = "F0FEF", Name = "Credit Card" },       // credit-card
                new AccountIcon { Code = "F0115", Name = "Cash" },              // cash-100
            ];
        }

        /// <summary>
        /// Gets the default icon code for accounts
        /// </summary>
        public string GetDefaultAccountIconCode()
        {
            return "F0070"; // bank
        }
    }
}
