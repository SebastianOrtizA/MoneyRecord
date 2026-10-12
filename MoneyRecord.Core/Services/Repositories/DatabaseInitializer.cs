using MoneyRecord.Models;
using MoneyRecord.Resources.Strings;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.Services.Repositories.Migrations;
using SQLite;

namespace MoneyRecord.Services.Repositories
{
    /// <summary>
    /// The app database: opens it on first use (creating the schema, running migrations and seeding default data)
    /// and hands out the connection. All access goes through <see cref="GetConnectionAsync"/> or
    /// <see cref="RunInTransactionAsync"/>, so nothing can use the database before it is ready.
    /// </summary>
    public sealed class DatabaseInitializer
    {
        private readonly string _databasePath;
        private readonly SemaphoreSlim _initLock = new(1, 1);
        private SQLiteAsyncConnection? _database;

        public DatabaseInitializer(IAppPaths paths)
        {
            _databasePath = paths.DatabasePath;
        }

        /// <summary>The open connection, initializing the database on the first call.</summary>
        public async Task<SQLiteAsyncConnection> GetConnectionAsync()
        {
            if (_database != null)
                return _database;

            await _initLock.WaitAsync();
            try
            {
                if (_database != null)
                    return _database;

                var db = new SQLiteAsyncConnection(_databasePath);
                await CreateSchemaAsync(db, new MigrationRunner(MigrationRunner.All));
                await SeedDefaultDataAsync(db);

                // Published only when ready: a concurrent caller never sees a half-initialized database
                _database = db;
                return db;
            }
            finally
            {
                _initLock.Release();
            }
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

        private static async Task SeedDefaultDataAsync(SQLiteAsyncConnection db)
        {
            if (await db.Table<Category>().CountAsync() == 0)
                await AddDefaultCategoriesAsync(db);

            if (await db.Table<Account>().CountAsync() == 0)
                await AddDefaultAccountAsync(db);
        }

        private static async Task AddDefaultCategoriesAsync(SQLiteAsyncConnection db)
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
                await db.InsertAsync(category);
            }
        }

        private static async Task AddDefaultAccountAsync(SQLiteAsyncConnection db)
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
            await db.InsertAsync(cashAccount);
        }
    }
}
