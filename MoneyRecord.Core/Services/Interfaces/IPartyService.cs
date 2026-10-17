using MoneyRecord.Models;

namespace MoneyRecord.Services.Interfaces
{
    /// <summary>
    /// Parties: shared events with their members, expenses and contributions, kept apart from the ledger. Money is checked
    /// here; anything that would leave a party inconsistent throws <see cref="PartyProblemException"/> (an <see cref="InvalidOperationException"/>) or
    /// <see cref="ArgumentException"/>.
    /// </summary>
    public interface IPartyService
    {
        /// <summary>The friends list, by name.</summary>
        Task<List<Participant>> GetParticipantsAsync();

        /// <summary>The friend with this name (<see cref="Helpers.PayeeNames.Same"/>), created when there is none.</summary>
        Task<Participant> AddParticipantAsync(string name);

        /// <exception cref="PartyProblemException">Another friend already has this name.</exception>
        Task RenameParticipantAsync(int participantId, string name);

        /// <exception cref="PartyProblemException">The friend is in a party.</exception>
        Task DeleteParticipantAsync(int participantId);

        /// <summary>Every party, the latest first.</summary>
        Task<List<PartyEvent>> GetPartiesAsync();

        Task<PartyEvent?> GetAsync(int partyId);

        /// <summary>Creates (Id 0) or updates a party with its members (at least one).</summary>
        /// <exception cref="PartyProblemException">A member with expenses or contributions is taken out.</exception>
        Task<int> SaveAsync(PartyEvent party, IReadOnlyCollection<int> memberIds);

        /// <summary>Deletes the party with its members, expenses and contributions.</summary>
        Task DeleteAsync(int partyId);

        Task<List<Participant>> GetMembersAsync(int partyId);

        /// <summary>The party's expenses, newest first.</summary>
        Task<List<PartyExpense>> GetExpensesAsync(int partyId);

        /// <summary>Expense ID → the member IDs left out of its split (only expenses with some).</summary>
        Task<Dictionary<int, HashSet<int>>> GetExclusionsAsync(int partyId);

        /// <summary>Creates (Id 0) or updates an expense; the payer (null = the pot) and the excluded must be members.</summary>
        Task<int> SaveExpenseAsync(PartyExpense expense, IReadOnlyCollection<int> excludedIds);

        Task DeleteExpenseAsync(int expenseId);

        /// <summary>The party's contributions, newest first.</summary>
        Task<List<PartyContribution>> GetContributionsAsync(int partyId);

        Task<int> SaveContributionAsync(PartyContribution contribution);

        Task DeleteContributionAsync(int contributionId);

        Task<PartySummary> GetSummaryAsync(int partyId);
    }
}
