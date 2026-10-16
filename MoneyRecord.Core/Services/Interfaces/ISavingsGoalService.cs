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

        /// <summary>Adds (ID 0) or updates the goal and replaces its accounts, in one database transaction.</summary>
        /// <exception cref="InvalidOperationException">
        /// An account is in another currency than the goal, or already belongs to another goal.
        /// </exception>
        Task SaveAsync(SavingsGoal goal, IReadOnlyCollection<int> accountIds);

        /// <summary>Deletes the goal; its accounts and their money stay as they are.</summary>
        Task DeleteAsync(int goalId);
    }
}
