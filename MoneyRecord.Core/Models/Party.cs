using MoneyRecord.Helpers;
using SQLite;

namespace MoneyRecord.Models
{
    /// <summary>A person on the friends list, reused across parties. Names are unique ignoring case (<see cref="PayeeNames"/>).</summary>
    public class Participant : IEntity
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    /// <summary>
    /// A shared event (party, dinner, group trip): standalone, not part of the ledger. Its expenses are split evenly
    /// between its members, minus the ones left out of an expense.
    /// </summary>
    public class PartyEvent : IEntity
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public DateTime Date { get; set; } = DateTime.Today;

        /// <summary>The event's one currency (empty = base).</summary>
        public string CurrencyCode { get; set; } = string.Empty;

        /// <summary>Money value for view models; stored as <see cref="BudgetCents"/>. Null = no budget.</summary>
        [Ignore]
        public decimal? Budget
        {
            get => BudgetCents is { } cents ? Money.FromCents(cents) : null;
            set => BudgetCents = value is { } amount ? Money.ToCents(amount) : null;
        }

        public long? BudgetCents { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }

    /// <summary>A participant taking part in a party.</summary>
    public class PartyMember : IEntity
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed(Name = "IX_PartyMember_PartyId")]
        public int PartyId { get; set; }

        public int ParticipantId { get; set; }
    }

    /// <summary>An expense of a party, paid by a participant or from the pot (<see cref="PaidByParticipantId"/> null).</summary>
    public class PartyExpense : IEntity
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed(Name = "IX_PartyExpense_PartyId")]
        public int PartyId { get; set; }

        public string Description { get; set; } = string.Empty;

        [Ignore]
        public decimal Amount
        {
            get => Money.FromCents(AmountCents);
            set => AmountCents = Money.ToCents(value);
        }

        public long AmountCents { get; set; }

        /// <summary>Who paid; null = the common pot (<see cref="PartyLedger.PotId"/>).</summary>
        public int? PaidByParticipantId { get; set; }

        public DateTime Date { get; set; } = DateTime.Today;
    }

    /// <summary>A member left out of one expense's split.</summary>
    public class PartyExpenseExclusion : IEntity
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed(Name = "IX_PartyExpenseExclusion_ExpenseId")]
        public int ExpenseId { get; set; }

        public int ParticipantId { get; set; }
    }

    /// <summary>Money a participant hands in to the common pot up front.</summary>
    public class PartyContribution : IEntity
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed(Name = "IX_PartyContribution_PartyId")]
        public int PartyId { get; set; }

        public int ParticipantId { get; set; }

        [Ignore]
        public decimal Amount
        {
            get => Money.FromCents(AmountCents);
            set => AmountCents = Money.ToCents(value);
        }

        public long AmountCents { get; set; }

        public DateTime Date { get; set; } = DateTime.Today;
    }

    /// <summary>One line of a party's balances: a participant (or the pot, ID 0) with what they paid, handed in, owe and net.</summary>
    /// <remarks><see cref="BalanceCents"/> = paid + contributed − share; positive = is owed, negative = owes.</remarks>
    public sealed record PartyBalance(int ParticipantId, string Name, long PaidCents, long ContributedCents, long ShareCents)
    {
        public long BalanceCents => PaidCents + ContributedCents - ShareCents;
    }

    /// <summary>A payment that settles part of a party: <paramref name="FromId"/> pays <paramref name="ToId"/> (0 = the pot).</summary>
    public sealed record SettlePayment(int FromId, int ToId, long AmountCents);

    /// <summary>A party at a glance: totals, per-person balances and the fewest payments that settle them.</summary>
    public sealed record PartySummary(PartyEvent Party, long TotalSpentCents, IReadOnlyList<PartyBalance> Balances,
        IReadOnlyList<SettlePayment> Payments)
    {
        public long? BudgetCents => Party.BudgetCents;

        public long? LeftCents => BudgetCents is { } budget ? budget - TotalSpentCents : null;
    }
}
