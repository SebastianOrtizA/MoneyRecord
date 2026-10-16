using MoneyRecord.Helpers;
using SQLite;

namespace MoneyRecord.Models
{
    /// <summary>
    /// A savings goal: reach <see cref="TargetAmount"/> in the balance of its linked accounts
    /// (<see cref="SavingsGoalAccount"/>), all in <see cref="CurrencyCode"/>, optionally by <see cref="TargetDate"/>.
    /// </summary>
    public class SavingsGoal : IEntity
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        /// <summary>Money value for view models; stored as <see cref="TargetAmountCents"/>.</summary>
        [Ignore]
        public decimal TargetAmount
        {
            get => Money.FromCents(TargetAmountCents);
            set => TargetAmountCents = Money.ToCents(value);
        }

        /// <summary>Stored value in cents (see <see cref="Money"/>).</summary>
        public long TargetAmountCents { get; set; }

        /// <summary>The goal's currency (a resolved code, never empty): only accounts in it can be linked.</summary>
        public string CurrencyCode { get; set; } = string.Empty;

        /// <summary>When the target should be reached; none = no deadline (only the projection is shown).</summary>
        public DateTime? TargetDate { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }

    /// <summary>An account whose balance counts towards a goal. An account belongs to one goal at most.</summary>
    public class SavingsGoalAccount : IEntity
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed(Name = "IX_SavingsGoalAccount_GoalId")]
        public int GoalId { get; set; }

        [Indexed(Name = "IX_SavingsGoalAccount_AccountId", Unique = true)]
        public int AccountId { get; set; }
    }
}
