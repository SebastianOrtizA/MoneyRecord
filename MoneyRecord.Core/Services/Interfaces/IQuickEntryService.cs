using MoneyRecord.Models;

namespace MoneyRecord.Services.Interfaces
{
    /// <summary>The user's quick entries (templates for a repeated expense or income) and the transactions they save.</summary>
    public interface IQuickEntryService
    {
        /// <summary>Every quick entry in the user's order, with names filled in.</summary>
        Task<List<QuickEntryItem>> GetAllAsync();

        Task<QuickEntry?> GetAsync(int id);

        /// <summary>Adds or updates a quick entry (new ones go last).</summary>
        /// <exception cref="InvalidOperationException">No name, a transfer, or a category or account that doesn't exist or doesn't fit the type.</exception>
        Task<int> SaveAsync(QuickEntry entry);

        /// <summary>
        /// A quick entry for a saved transaction (not saved): named after its payee, description or category.
        /// Null for a transfer, a trip expense, or a transaction that is gone.
        /// </summary>
        Task<QuickEntry?> DraftFromTransactionAsync(int transactionId);

        /// <summary>
        /// A draft for the transaction just saved when the user has repeated the same payee, category, account and amount at
        /// least three times in the last 60 days and no quick entry covers it; otherwise null.
        /// </summary>
        Task<QuickEntry?> SuggestAsync(int transactionId);

        Task DeleteAsync(int id);

        /// <summary>Moves it one place up or down in the user's order.</summary>
        Task MoveAsync(int id, bool up);

        /// <summary>
        /// Saves the quick entry as a transaction dated now and counts the use. <paramref name="amountCents"/> replaces the stored
        /// amount (required when it asks for one).
        /// </summary>
        /// <exception cref="InvalidOperationException">It is gone, its category or account is, or there is no amount.</exception>
        Task<Transaction> UseAsync(int id, long? amountCents);
    }
}
