using MoneyRecord.Models;

namespace MoneyRecord.Services.Interfaces
{
    /// <summary>Money to take out of one pocket to cover an entry.</summary>
    public sealed record PocketTake(int PocketId, decimal Amount);

    /// <summary>
    /// Pockets: money set aside inside an account. Only an earmark: balances, totals, reports and transfers keep
    /// counting the real account.
    /// </summary>
    public interface IPocketService
    {
        /// <summary>What each pocket holds, in cents (pocket ID → sum of its movements).</summary>
        Task<Dictionary<int, long>> GetBalancesAsync();

        /// <summary>What each account has in pockets, in cents (account ID → total); accounts without pockets are absent.</summary>
        Task<Dictionary<int, long>> GetTotalsByAccountAsync();

        /// <summary>The pockets of <paramref name="accountId"/> (all accounts when null), by account then name.</summary>
        Task<List<PocketSummary>> GetPocketsAsync(int? accountId = null);

        /// <summary>How much each pocket changed through the movements dated on or after <paramref name="since"/>, in cents; pockets without any are absent.</summary>
        Task<Dictionary<int, long>> GetChangesSinceAsync(DateTime since);

        /// <summary>The pocket's movements, newest first.</summary>
        Task<List<PocketMovement>> GetMovementsAsync(int pocketId);

        /// <summary>Adds (ID 0) or updates the pocket.</summary>
        /// <exception cref="PocketProblemException">
        /// The account is gone or is a debt's account, or an existing pocket would move to another account.
        /// </exception>
        Task SaveAsync(Pocket pocket);

        /// <summary>Deletes the pocket with its movements and goal link; the money simply goes back to available.</summary>
        Task DeleteAsync(int pocketId);

        /// <summary>
        /// Sets money aside (positive) or takes it out (negative).
        /// </summary>
        /// <exception cref="PocketProblemException">
        /// Setting aside more than the account has available, or taking out more than the pocket holds.
        /// </exception>
        Task MoveAsync(int pocketId, decimal amount, DateTime date, string? note = null);

        /// <summary>
        /// How much the account's pockets would exceed its balance after another <paramref name="extraOutflow"/>
        /// leaves it on <paramref name="date"/> (0 for a future-dated entry: it is in no balance yet).
        /// </summary>
        Task<decimal> GetShortfallAsync(int accountId, decimal extraOutflow, DateTime date);

        /// <summary>Records the take-outs of <paramref name="takes"/> (all from pockets of <paramref name="accountId"/>), linked to the entry they covered, in one transaction.</summary>
        Task CoverAsync(int accountId, IReadOnlyList<PocketTake> takes, DateTime date, int? transactionId = null, int? transferId = null);
    }
}
