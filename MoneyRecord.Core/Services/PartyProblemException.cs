namespace MoneyRecord.Services
{
    /// <summary>Why a change to a party or the friends list was refused, so a view model can say it in the user's language.</summary>
    public enum PartyProblem
    {
        /// <summary>The friend's name is taken by another friend.</summary>
        NameTaken,
        /// <summary>The friend is in a party.</summary>
        FriendInParty,
        /// <summary>A member with expenses or contributions would be taken out of a party.</summary>
        MemberInUse,
        /// <summary>Taking a member out would leave an expense nobody shares.</summary>
        NobodyShares,
        /// <summary>The owner's share is zero, so there is nothing to record.</summary>
        NoShare,
        /// <summary>The account's currency isn't the party's.</summary>
        AccountCurrency,
        /// <summary>The recorded share expense is gone and no account or category was given; the link has been cleared.</summary>
        ShareDeleted
    }

    public sealed class PartyProblemException(PartyProblem problem, string message) : InvalidOperationException(message)
    {
        public PartyProblem Problem { get; } = problem;
    }
}
