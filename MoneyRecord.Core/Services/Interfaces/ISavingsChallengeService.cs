using MoneyRecord.Models;

namespace MoneyRecord.Services.Interfaces
{
    /// <summary>Savings challenges: the plan's day amounts, the days done and the optional transfers they record.</summary>
    public interface ISavingsChallengeService
    {
        /// <summary>Every challenge's progress: open ones first (newest first), finished ones last.</summary>
        Task<List<ChallengeSummary>> GetSummariesAsync();

        Task<ChallengeSummary?> GetSummaryAsync(int challengeId);

        /// <summary>Adds (ID 0) or updates the challenge.</summary>
        /// <exception cref="InvalidOperationException">
        /// An account is gone or in another currency than the challenge, or only one is set; or a day is done and the goal,
        /// days, mode or rounding changed.
        /// </exception>
        Task SaveAsync(SavingsChallenge challenge);

        /// <summary>Deletes the challenge and its days; the transfers they recorded stay.</summary>
        Task DeleteAsync(int challengeId);

        /// <summary>Marks a day done today with its planned amount; with <paramref name="recordTransfer"/> also records the transfer.</summary>
        /// <exception cref="InvalidOperationException">The day is out of range or done, or a transfer needs accounts it does not have.</exception>
        Task MarkDoneAsync(int challengeId, int dayNumber, bool recordTransfer);

        /// <summary>Marks a day not done; with <paramref name="deleteTransfer"/> also deletes the transfer it recorded.</summary>
        Task UnmarkAsync(int challengeId, int dayNumber, bool deleteTransfer);
    }
}
