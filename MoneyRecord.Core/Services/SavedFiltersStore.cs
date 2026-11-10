using MoneyRecord.Models;
using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Services
{
    public sealed class SavedFiltersStore(IPreferencesService preferences) : ISavedFiltersStore
    {
        private const string Key = "Transactions.SavedFilters";

        public List<SavedFilter> Load() => SavedFilters.Parse(preferences.Get(Key, string.Empty));

        public void Save(IEnumerable<SavedFilter> filters) => preferences.Set(Key, SavedFilters.Serialize(filters));
    }
}
