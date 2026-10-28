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
                new CategoryIcon { Code = "F140B", Name = "Utilities" },        // lightning-bolt
                new CategoryIcon { Code = "F03E9", Name = "Pet" },              // paw
                new CategoryIcon { Code = "F02E1", Name = "Medical" },          // hospital-building

                // Food and drink
                new CategoryIcon { Code = "F0A70", Name = "Restaurant" },       // silverware-fork-knife
                new CategoryIcon { Code = "F0176", Name = "Coffee" },           // coffee
                new CategoryIcon { Code = "F0409", Name = "Pizza" },            // pizza
                new CategoryIcon { Code = "F0CEE", Name = "Bakery" },           // bread-slice
                new CategoryIcon { Code = "F00EB", Name = "Cake" },             // cake-variant
                new CategoryIcon { Code = "F0876", Name = "Wine" },             // glass-wine
                new CategoryIcon { Code = "F0098", Name = "Beer" },             // beer

                // Transport
                new CategoryIcon { Code = "F0298", Name = "Fuel" },             // gas-station
                new CategoryIcon { Code = "F00E7", Name = "Bus" },              // bus
                new CategoryIcon { Code = "F04FF", Name = "Taxi" },             // taxi
                new CategoryIcon { Code = "F037C", Name = "Motorbike" },        // motorbike
                new CategoryIcon { Code = "F109C", Name = "Bicycle" },          // bicycle
                new CategoryIcon { Code = "F1814", Name = "Car repair" },       // car-wrench
                new CategoryIcon { Code = "F03E3", Name = "Parking" },          // parking
                new CategoryIcon { Code = "F001D", Name = "Flight" },           // airplane

                // Home and bills
                new CategoryIcon { Code = "F04B9", Name = "Furniture" },        // sofa
                new CategoryIcon { Code = "F058C", Name = "Water" },            // water
                new CategoryIcon { Code = "F0238", Name = "Heating" },          // fire
                new CategoryIcon { Code = "F05A9", Name = "Internet" },         // wifi
                new CategoryIcon { Code = "F011C", Name = "Phone" },            // cellphone
                new CategoryIcon { Code = "F0502", Name = "TV" },               // television
                new CategoryIcon { Code = "F072A", Name = "Laundry" },          // washing-machine
                new CategoryIcon { Code = "F1323", Name = "Repairs" },          // hammer-wrench
                new CategoryIcon { Code = "F0565", Name = "Insurance" },        // shield-check
                new CategoryIcon { Code = "F0824", Name = "Receipt" },          // receipt

                // Health and personal
                new CategoryIcon { Code = "F0402", Name = "Pharmacy" },         // pill
                new CategoryIcon { Code = "F08C3", Name = "Dental" },           // tooth
                new CategoryIcon { Code = "F05F6", Name = "Health" },           // heart-pulse
                new CategoryIcon { Code = "F0190", Name = "Haircut" },          // content-cut
                new CategoryIcon { Code = "F01E6", Name = "Gym" },              // dumbbell
                new CategoryIcon { Code = "F0A7B", Name = "Clothing" },         // tshirt-crew
                new CategoryIcon { Code = "F0B47", Name = "Shoes" },            // shoe-formal
                new CategoryIcon { Code = "F0E10", Name = "Bag" },              // bag-personal

                // Fun, family and giving
                new CategoryIcon { Code = "F0FCE", Name = "Movies" },           // movie-open
                new CategoryIcon { Code = "F0297", Name = "Games" },            // gamepad-variant
                new CategoryIcon { Code = "F075A", Name = "Music" },            // music
                new CategoryIcon { Code = "F04B8", Name = "Sports" },           // soccer
                new CategoryIcon { Code = "F0516", Name = "Tickets" },          // ticket
                new CategoryIcon { Code = "F1056", Name = "Party" },            // party-popper
                new CategoryIcon { Code = "F0092", Name = "Vacation" },         // beach
                new CategoryIcon { Code = "F0100", Name = "Photography" },      // camera
                new CategoryIcon { Code = "F0322", Name = "Laptop" },           // laptop
                new CategoryIcon { Code = "F0474", Name = "School" },           // school
                new CategoryIcon { Code = "F14F7", Name = "Books" },            // book-open-variant
                new CategoryIcon { Code = "F068F", Name = "Baby" },             // baby-carriage
                new CategoryIcon { Code = "F0E44", Name = "Gift" },             // gift
                new CategoryIcon { Code = "F024A", Name = "Flowers" },          // flower
                new CategoryIcon { Code = "F10F1", Name = "Donation" },         // hand-heart
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

                // Work
                new CategoryIcon { Code = "F1097", Name = "Wages" },            // account-cash
                new CategoryIcon { Code = "F1261", Name = "Bonus" },            // cash-plus
                new CategoryIcon { Code = "F0A2B", Name = "Side job" },         // briefcase-plus
                new CategoryIcon { Code = "F1A4A", Name = "Online work" },      // laptop-account
                new CategoryIcon { Code = "F04DC", Name = "Sales" },            // store
                new CategoryIcon { Code = "F1218", Name = "Deals" },            // handshake

                // Money in
                new CategoryIcon { Code = "F188F", Name = "Payment" },          // hand-coin
                new CategoryIcon { Code = "F0584", Name = "Wallet" },           // wallet
                new CategoryIcon { Code = "F0070", Name = "Bank" },             // bank
                new CategoryIcon { Code = "F0A27", Name = "Transfer" },         // bank-transfer
                new CategoryIcon { Code = "F0A9C", Name = "Refund" },           // cash-refund
                new CategoryIcon { Code = "F046F", Name = "Sale" },             // sale
                new CategoryIcon { Code = "F0E44", Name = "Gift" },             // gift
                new CategoryIcon { Code = "F0D15", Name = "Rent" },             // home-city
                new CategoryIcon { Code = "F0474", Name = "Scholarship" },      // school

                // Investments and savings
                new CategoryIcon { Code = "F012A", Name = "Chart" },            // chart-line
                new CategoryIcon { Code = "F0535", Name = "Growth" },           // trending-up
                new CategoryIcon { Code = "F03F0", Name = "Interest" },         // percent
                new CategoryIcon { Code = "F1007", Name = "Savings" },          // piggy-bank
                new CategoryIcon { Code = "F0726", Name = "Treasure" },         // treasure-chest
                new CategoryIcon { Code = "F0E66", Name = "Sprout" },           // sprout
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
            return type == CategoryType.Income ? "F0116" : "F025A"; // cash-multiple or food
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
