using MoneyRecord.Models;

namespace MoneyRecord.Services.Interfaces
{
    /// <summary>The Transactions filters the user saved by name; kept in preferences (no table, not in a backup).</summary>
    public interface ISavedFiltersStore
    {
        List<SavedFilter> Load();

        void Save(IEnumerable<SavedFilter> filters);
    }
}
