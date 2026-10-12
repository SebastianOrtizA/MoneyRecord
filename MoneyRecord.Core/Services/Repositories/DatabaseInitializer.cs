using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.Services.Repositories.Migrations;
using SQLite;

namespace MoneyRecord.Services.Repositories
{
    /// <summary>
    /// SQLite implementation of the database initializer.
    /// Handles database creation, table setup, and default data seeding.
    /// </summary>
    public sealed class DatabaseInitializer : IDatabaseInitializer
    {
        private readonly string _databasePath;
        private readonly SemaphoreSlim _initLock = new(1, 1);
        private bool _isInitialized;

        public SQLiteAsyncConnection? Database { get; private set; }
        public bool IsInitialized => _isInitialized;

        public DatabaseInitializer(IAppPaths paths)
        {
            _databasePath = paths.DatabasePath;
        }

        public async Task InitializeAsync()
        {
            if (_isInitialized)
                return;

            await _initLock.WaitAsync();
            try
            {
                if (_isInitialized)
                    return;

                Database = new SQLiteAsyncConnection(_databasePath);

                await CreateSchemaAsync(Database, new MigrationRunner(MigrationRunner.All));

                await SeedDefaultDataAsync();

                _isInitialized = true;
            }
            finally
            {
                _initLock.Release();
            }
        }

        /// <summary>The open connection, initializing the database first if needed.</summary>
        public async Task<SQLiteAsyncConnection> GetConnectionAsync()
        {
            await InitializeAsync();
            return Database!;
        }

        /// <summary>
        /// Runs <paramref name="work"/> in one SQLite transaction: all of it is written, or nothing when it throws.
        /// For changes that span tables (delete cascades, restore) or a consistent read of several tables.
        /// </summary>
        public async Task RunInTransactionAsync(Action<SQLiteConnection> work)
        {
            var db = await GetConnectionAsync();
            await db.RunInTransactionAsync(work);
        }

        /// <summary>
        /// Creates or updates the tables from the models (latest schema). A new database is stamped with
        /// the latest version; a database from an older app version runs its pending migrations.
        /// </summary>
        public static async Task CreateSchemaAsync(SQLiteAsyncConnection db, MigrationRunner migrations)
        {
            var isNewDatabase = await db.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table'") == 0;

            await db.CreateTableAsync<Category>();
            await db.CreateTableAsync<Transaction>();
            await db.CreateTableAsync<Account>();
            await db.CreateTableAsync<Transfer>();
            await db.CreateTableAsync<Budget>();
            await db.CreateTableAsync<RecurringTransaction>();

            if (isNewDatabase)
                await migrations.StampLatestAsync(db);
            else
                await migrations.RunAsync(db);
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
                IconCode = "F0115",
                // Cash starts at 0, so a new user's first expense must not be blocked
                AllowNegativeBalance = true,
                CurrencyCode = CurrencyService.GetDeviceCurrencyCode()
            };
            await Database!.InsertAsync(cashAccount);
        }
    }
}
