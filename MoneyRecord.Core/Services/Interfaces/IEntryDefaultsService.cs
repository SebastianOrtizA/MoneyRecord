using MoneyRecord.Models;

namespace MoneyRecord.Services.Interfaces
{
    /// <summary>
    /// What the add form starts with: the account and category last used for each type, and recent categories first.
    /// </summary>
    public interface IEntryDefaultsService
    {
        /// <summary>The account last used for <paramref name="type"/> if it still exists, else the default account, else the first.</summary>
        Account? DefaultAccount(TransactionType type, IEnumerable<Account> accounts);

        /// <summary>The category last used for <paramref name="type"/> if it still exists, else the first of <paramref name="categories"/>.</summary>
        Category? DefaultCategory(TransactionType type, IEnumerable<Category> categories);

        /// <summary>The recently used categories of <paramref name="type"/> first (most recent first), then the rest in their order.</summary>
        List<Category> OrderByRecent(TransactionType type, IEnumerable<Category> categories);

        /// <summary>Remembers a saved entry's account and category for the next one of <paramref name="type"/>.</summary>
        void Remember(TransactionType type, int accountId, int categoryId);
    }
}
