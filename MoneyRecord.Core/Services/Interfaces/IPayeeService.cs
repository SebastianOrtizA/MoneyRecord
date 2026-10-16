using MoneyRecord.Models;

namespace MoneyRecord.Services.Interfaces
{
    /// <summary>Payees: found or created by name when an entry is saved, with their use counts; renamed, merged and deleted.</summary>
    public interface IPayeeService
    {
        /// <summary>Every payee with its transaction count and last use, by name.</summary>
        Task<List<PayeeSummary>> GetAllAsync();

        /// <summary>
        /// The payee with this name (<see cref="Helpers.PayeeNames.Same"/>), created when there is none; null for an empty name.
        /// </summary>
        Task<int?> ResolveAsync(string? name);

        /// <summary>The category of the payee's latest transaction of this type, if it has one.</summary>
        Task<int?> GetLastCategoryAsync(int payeeId, TransactionType type);

        /// <summary>Gives the payee a new name.</summary>
        /// <exception cref="InvalidOperationException">The name is empty or another payee has it (merge them instead).</exception>
        Task RenameAsync(int payeeId, string name);

        /// <summary>Moves the payee's transactions and recurring transactions to <paramref name="targetId"/>, then deletes it.</summary>
        Task MergeAsync(int payeeId, int targetId);

        /// <summary>Deletes the payee; its transactions stay, without a payee.</summary>
        Task DeleteAsync(int payeeId);
    }
}
