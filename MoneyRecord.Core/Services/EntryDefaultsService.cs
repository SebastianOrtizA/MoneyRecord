using MoneyRecord.Models;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Services
{
    /// <summary>Keeps the last account and the recent categories per type in preferences (IDs, so renames don't matter).</summary>
    public class EntryDefaultsService : IEntryDefaultsService
    {
        /// <summary>How many recent categories go to the front of the grid.</summary>
        public const int RecentCount = 4;

        private readonly IPreferencesService _preferences;

        public EntryDefaultsService(IPreferencesService preferences)
        {
            _preferences = preferences;
        }

        public Account? DefaultAccount(TransactionType type, IEnumerable<Account> accounts)
        {
            var lastId = _preferences.Get(AccountKey(type), 0);
            return accounts.FirstOrDefault(a => a.Id == lastId)
                   ?? accounts.FirstOrDefault(a => a.IsDefault)
                   ?? accounts.FirstOrDefault();
        }

        public Category? DefaultCategory(TransactionType type, IEnumerable<Category> categories)
        {
            var recent = RecentCategoryIds(type);
            var last = recent.Count > 0 ? categories.FirstOrDefault(c => c.Id == recent[0]) : null;
            return last ?? categories.FirstOrDefault();
        }

        public List<Category> OrderByRecent(TransactionType type, IEnumerable<Category> categories)
        {
            var recent = RecentCategoryIds(type);
            return categories
                .Select((category, index) => (category, index, rank: recent.IndexOf(category.Id)))
                .OrderBy(c => c.rank < 0 ? int.MaxValue : c.rank)
                .ThenBy(c => c.index)
                .Select(c => c.category)
                .ToList();
        }

        public void Remember(TransactionType type, int accountId, int categoryId)
        {
            _preferences.Set(AccountKey(type), accountId);

            var recent = RecentCategoryIds(type);
            recent.Remove(categoryId);
            recent.Insert(0, categoryId);
            _preferences.Set(RecentCategoriesKey(type), string.Join(',', recent.Take(RecentCount)));
        }

        private List<int> RecentCategoryIds(TransactionType type) =>
            _preferences.Get(RecentCategoriesKey(type), string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(id => int.TryParse(id, out var value) ? value : 0)
                .Where(id => id > 0)
                .ToList();

        private static string AccountKey(TransactionType type) => $"Entry.LastAccount.{type}";

        private static string RecentCategoriesKey(TransactionType type) => $"Entry.RecentCategories.{type}";
    }
}
