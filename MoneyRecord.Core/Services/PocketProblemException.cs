namespace MoneyRecord.Services
{
    /// <summary>Why a change to a pocket was refused, so a view model can say it in the user's language.</summary>
    public enum PocketProblem
    {
        /// <summary>Setting aside more than the account has available (balance minus what its pockets hold).</summary>
        NotEnoughAvailable,
        /// <summary>Taking out more than the pocket holds.</summary>
        NotEnoughInPocket,
        /// <summary>The pocket's account is gone.</summary>
        AccountNotFound,
        /// <summary>The pocket is gone.</summary>
        PocketNotFound,
        /// <summary>A debt's hidden account can't have pockets.</summary>
        DebtAccount
    }

    public sealed class PocketProblemException(PocketProblem problem, string message) : InvalidOperationException(message)
    {
        public PocketProblem Problem { get; } = problem;
    }
}
