using MoneyRecord.Models;
using MoneyRecord.Services.Interfaces;
using MoneyRecord.Services.Repositories;

namespace MoneyRecord.Services
{
    public sealed class SetupService : ISetupService
    {
        public const string SetupStatePreferenceKey = "SetupState";
        private const string Pending = "pending";
        private const string Done = "done";

        private readonly IPreferencesService _preferences;
        private readonly IAppPaths _paths;
        private readonly DatabaseInitializer _dbInitializer;
        private readonly IAccountRepository _accountRepository;
        private readonly ICategoryRepository _categoryRepository;

        public SetupService(IPreferencesService preferences, IAppPaths paths, DatabaseInitializer dbInitializer,
            IAccountRepository accountRepository, ICategoryRepository categoryRepository)
        {
            _preferences = preferences;
            _paths = paths;
            _dbInitializer = dbInitializer;
            _accountRepository = accountRepository;
            _categoryRepository = categoryRepository;
        }

        public bool ResolveFirstRunPending()
        {
            var state = _preferences.Get(SetupStatePreferenceKey, string.Empty);
            if (state.Length == 0)
            {
                // Decided once, so closing the app halfway through the setup shows it again
                state = File.Exists(_paths.DatabasePath) ? Done : Pending;
                _preferences.Set(SetupStatePreferenceKey, state);
            }
            return state == Pending;
        }

        public async Task ApplyAsync(SetupChoices choices)
        {
            try
            {
                await _dbInitializer.RunInTransactionAsync(db =>
                {
                    // Only the untouched seed data is replaced: anything recorded keeps its accounts and categories
                    var replace = choices.ReplaceStartingData
                        && db.Table<Transaction>().Count() == 0
                        && db.Table<Transfer>().Count() == 0
                        && db.Table<Budget>().Count() == 0
                        && db.Table<RecurringTransaction>().Count() == 0;

                    if (replace)
                    {
                        if (choices.Accounts.Count == 0)
                            throw new ArgumentException("The setup needs at least one account.", nameof(choices));

                        db.DeleteAll<Account>();
                        db.DeleteAll<Category>();
                    }

                    var existing = db.Table<Category>().ToList();
                    foreach (var category in choices.Categories)
                    {
                        if (!existing.Any(category.Matches))
                            db.Insert(category.ToCategory());
                    }

                    var needsDefault = db.Table<Account>().Count(a => a.IsDefault) == 0;
                    foreach (var account in choices.Accounts)
                    {
                        db.Insert(new Account
                        {
                            Name = account.Name,
                            InitialBalance = account.InitialBalance,
                            IconCode = account.IconCode,
                            CurrencyCode = choices.CurrencyCode,
                            IsDefault = needsDefault,
                            // Like the seeded Cash account: a new user's first expense must not be blocked
                            AllowNegativeBalance = true,
                        });
                        needsDefault = false;
                    }
                });
            }
            finally
            {
                _accountRepository.InvalidateCache();
                _categoryRepository.InvalidateCache();
            }

            _preferences.Set(SetupStatePreferenceKey, Done);
        }
    }
}
