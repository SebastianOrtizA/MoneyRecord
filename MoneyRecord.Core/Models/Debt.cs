using SQLite;

namespace MoneyRecord.Models
{
    /// <summary>Which way the money went when the debt started.</summary>
    public enum DebtDirection
    {
        /// <summary>The user lent it: the person owes the user.</summary>
        Lent,

        /// <summary>The user borrowed it: the user owes the person.</summary>
        Borrowed
    }

    /// <summary>
    /// Money lent to or borrowed from a person (<see cref="PayeeId"/>). Its money lives in a hidden account of its own
    /// (<see cref="AccountId"/>, <see cref="Account.IsDebt"/>, in the debt's currency): lending, borrowing and every payment
    /// are transfers between it and a real account, so balances and reports stay right without debt rules of their own.
    /// A debt that was owed before it was recorded starts as that account's initial balance (no account involved).
    /// Optional payment schedule = a <see cref="RecurringTransaction"/> with this <see cref="RecurringTransaction.DebtId"/>.
    /// </summary>
    public class Debt : IEntity
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed(Name = "IX_Debt_PayeeId")]
        public int PayeeId { get; set; }

        public DebtDirection Direction { get; set; }

        /// <summary>The debt's hidden account (one per debt).</summary>
        [Indexed(Name = "IX_Debt_AccountId", Unique = true)]
        public int AccountId { get; set; }

        /// <summary>What it was for ("Car repair"); empty = none.</summary>
        public string Note { get; set; } = string.Empty;

        /// <summary>When it should be paid back; none = no deadline. A reminder comes before it while money is left.</summary>
        public DateTime? DueDate { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }
}
