using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services.Interfaces;
using SQLite;

namespace MoneyRecord.Services.Repositories
{
    /// <summary>
    /// SQLite implementation of the database initializer.
    /// Handles database creation, table setup, and default data seeding.
    /// </summary>
    public sealed class DatabaseInitializer : IDatabaseInitializer
    {
        private readonly SemaphoreSlim _initLock = new(1, 1);
        private bool _isInitialized;

        public SQLiteAsyncConnection? Database { get; private set; }
        public bool IsInitialized => _isInitialized;

        public async Task InitializeAsync()
        {
            if (_isInitialized)
                return;

            await _initLock.WaitAsync();
            try
            {
                if (_isInitialized)
                    return;

                var dbPath = Path.Combine(FileSystem.AppDataDirectory, "moneyrecord.db3");
                Database = new SQLiteAsyncConnection(dbPath);

                await Database.CreateTableAsync<Category>();
                await Database.CreateTableAsync<Transaction>();
                await Database.CreateTableAsync<Account>();
                await Database.CreateTableAsync<Transfer>();

                await SeedDefaultDataAsync();

                _isInitialized = true;
            }
            finally
            {
                _initLock.Release();
            }
        }

        private async Task SeedDefaultDataAsync()
        {
            if (Database == null) return;

            var categoryCount = await Database.Table<Category>().CountAsync();
            if (categoryCount == 0)
            {
                await AddDefaultCategoriesAsync();
            }

            var accountCount = await Database.Table<Account>().CountAsync();
            if (accountCount == 0)
            {
                await AddDefaultAccountAsync();
            }
        }

        private async Task AddDefaultCategoriesAsync()
        {
            var defaultIncomeCategories = new[]
            {
                new Category { Name = AppResources.DefaultCategorySalary, Type = CategoryType.Income, IconCode = "F0116" },
                new Category { Name = AppResources.DefaultCategoryFreelance, Type = CategoryType.Income, IconCode = "F00D6" },
                new Category { Name = AppResources.DefaultCategoryInvestment, Type = CategoryType.Income, IconCode = "F081F" },
                new Category { Name = AppResources.DefaultCategoryOtherIncome, Type = CategoryType.Income, IconCode = "F0CF4" }
            };

            var defaultExpenseCategories = new[]
            {
                new Category { Name = AppResources.DefaultCategoryFood, Type = CategoryType.Expense, IconCode = "F025A" },
                new Category { Name = AppResources.DefaultCategoryTransportation, Type = CategoryType.Expense, IconCode = "F0BD8" },
                new Category { Name = AppResources.DefaultCategoryEntertainment, Type = CategoryType.Expense, IconCode = "F0356" },
                new Category { Name = AppResources.DefaultCategoryUtilities, Type = CategoryType.Expense, IconCode = "F0D15" },
                new Category { Name = AppResources.DefaultCategoryShopping, Type = CategoryType.Expense, IconCode = "F0110" },
                new Category { Name = AppResources.DefaultCategoryHome, Type = CategoryType.Expense, IconCode = "F0D15" },
                new Category { Name = AppResources.DefaultCategoryOtherExpense, Type = CategoryType.Expense, IconCode = "F0076" }
            };

            foreach (var category in defaultIncomeCategories.Concat(defaultExpenseCategories))
            {
                await Database!.InsertAsync(category);
            }
        }

        private async Task AddDefaultAccountAsync()
        {
            var cashAccount = new Account
            {
                Name = AppResources.DefaultAccountCash,
                InitialBalance = 0,
                IsDefault = true,
                IconCode = "F0115"
            };
            await Database!.InsertAsync(cashAccount);
        }
    }
}
