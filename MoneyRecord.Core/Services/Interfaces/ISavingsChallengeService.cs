using MoneyRecord.Helpers;
using MoneyRecord.Models;

namespace MoneyRecord.Services.Interfaces
{
    /// <summary>Savings challenges: the plan's day amounts, the days done and the money they move.</summary>
    public interface ISavingsChallengeService
    {
        /// <summary>Every challenge's progress: open ones first (newest first), finished ones last.</summary>
        Task<List<ChallengeSummary>> GetSummariesAsync();

        Task<ChallengeSummary?> GetSummaryAsync(int challengeId);

        /// <summary>Adds (ID 0) or updates the challenge.</summary>
        /// <exception cref="InvalidOperationException">
        /// The destination is gone or in another currency than the challenge; or a day is done and the goal, days, mode,
        /// rounding or currency changed. A destination pocket sets the destination account.
        /// </exception>
        Task SaveAsync(SavingsChallenge challenge);

        /// <summary>Deletes the challenge and its days; the money they moved stays.</summary>
        Task DeleteAsync(int challengeId);

        /// <summary>
        /// Marks a day done today with its planned amount. With a <paramref name="source"/> (an account or a pocket, see
        /// <see cref="ChallengeSources"/>) it also moves the money to the challenge's destination, in one DB transaction:
        /// a transfer between different accounts, a take-out from a source pocket, a set-aside in a destination pocket.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The day is out of range or done; the challenge has no destination, or the route moves nothing or crosses currencies.
        /// </exception>
        /// <exception cref="PocketProblemException">The source pocket holds less, or the account has less available to set aside.</exception>
        Task MarkDoneAsync(int challengeId, int dayNumber, ChallengePlace? source);

        /// <summary>Marks a day not done; with <paramref name="undoMoney"/> also deletes the transfer and pocket movements it made.</summary>
        Task UnmarkAsync(int challengeId, int dayNumber, bool undoMoney);
    }
}
