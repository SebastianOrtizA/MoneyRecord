using MoneyRecord.Models;

namespace MoneyRecord.Services.Interfaces
{
    /// <summary>Savings goals: progress = the balance of each goal's linked accounts, plus the projection.</summary>
    public interface ISavingsGoalService
    {
        /// <summary>Every goal's progress: open goals first (earliest target date first, no date last), reached ones last.</summary>
        /// <param name="balances">Every account's balance when the caller has them already (Home); loaded otherwise.</param>
        Task<List<GoalProgress>> GetProgressAsync(IReadOnlyList<AccountBalanceInfo>? balances = null);

        /// <summary>Which goal each linked account belongs to (account ID → goal ID).</summary>
        Task<Dictionary<int, int>> GetLinkedAccountsAsync();

        /// <summary>Which goal each linked pocket belongs to (pocket ID → goal ID).</summary>
        Task<Dictionary<int, int>> GetLinkedPocketsAsync();

        /// <summary>Adds (ID 0) or updates the goal and replaces its accounts and pockets, in one database transaction.</summary>
        /// <param name="pocketIds">The goal's pockets; null keeps the ones it has.</param>
        /// <exception cref="InvalidOperationException">
        /// An account or pocket is in another currency than the goal, or already belongs to another goal; a pocket is
        /// linked together with its own account (its money would count twice).
        /// </exception>
        Task SaveAsync(SavingsGoal goal, IReadOnlyCollection<int> accountIds, IReadOnlyCollection<int>? pocketIds = null);

        /// <summary>Deletes the goal; its accounts and their money stay as they are.</summary>
        Task DeleteAsync(int goalId);
    }
}
