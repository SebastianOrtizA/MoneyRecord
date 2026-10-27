using MoneyRecord.Helpers;
using SQLite;

namespace MoneyRecord.Models
{
    /// <summary>How a challenge's day amounts grow.</summary>
    public enum ChallengeMode
    {
        /// <summary>a, 2a, 4a, ... (in levels when the days outnumber the useful doublings).</summary>
        Doubling,
        /// <summary>Day i saves i times the same step.</summary>
        Linear,
        /// <summary>Every day saves the same amount (the last day may take the remainder).</summary>
        Equal
    }

    /// <summary>
    /// A savings game: reach <see cref="GoalCents"/> in <see cref="Days"/> days from <see cref="StartDate"/>. The day
    /// amounts come from <see cref="ChallengePlan"/>; each day done keeps its own amount (<see cref="SavingsChallengeDay"/>).
    /// </summary>
    public class SavingsChallenge : IEntity
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        /// <summary>Money value for view models; stored as <see cref="GoalCents"/>.</summary>
        [Ignore]
        public decimal Goal
        {
            get => Money.FromCents(GoalCents);
            set => GoalCents = Money.ToCents(value);
        }

        /// <summary>Stored value in cents (see <see cref="Money"/>).</summary>
        public long GoalCents { get; set; }

        /// <summary>The challenge's currency (a resolved code, never empty): only accounts and pockets in it can be the destination.</summary>
        public string CurrencyCode { get; set; } = string.Empty;

        public DateTime StartDate { get; set; }

        public int Days { get; set; }

        public ChallengeMode Mode { get; set; }

        /// <summary>Each day's amount is rounded to a multiple of this (cents); 0 = cents.</summary>
        public long RoundingCents { get; set; }

        /// <summary>Equal mode: the fixed amount saved each day (cents), the days then follow from the goal; 0 = the goal split over <see cref="Days"/>.</summary>
        public long DayAmountCents { get; set; }

        /// <summary>
        /// Where the saved money ends up: this account (or, with <see cref="ToPocketId"/>, a pocket of it). None = days are
        /// only marked. Each day asks where its money comes from.
        /// </summary>
        public int? ToAccountId { get; set; }

        public int? ToPocketId { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        [Ignore]
        public bool HasDestination => ToAccountId != null;
    }

    /// <summary>One day done: its own amount (the history stays right if the plan changes) and the money it moved, if any.</summary>
    public class SavingsChallengeDay : IEntity
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed(Name = "IX_SavingsChallengeDay_Day", Unique = true, Order = 1)]
        public int ChallengeId { get; set; }

        /// <summary>1-based.</summary>
        [Indexed(Name = "IX_SavingsChallengeDay_Day", Unique = true, Order = 2)]
        public int DayNumber { get; set; }

        public long AmountCents { get; set; }

        public DateTime DoneDate { get; set; }

        /// <summary>The transfer between accounts this day recorded, if any (it can be gone: deleted on the Transfers page).</summary>
        public int? TransferId { get; set; }

        /// <summary>The take-out from the source pocket, if the money came from one.</summary>
        public int? SourceMovementId { get; set; }

        /// <summary>The set-aside in the destination pocket, if the money went to one.</summary>
        public int? DestinationMovementId { get; set; }
    }
}
