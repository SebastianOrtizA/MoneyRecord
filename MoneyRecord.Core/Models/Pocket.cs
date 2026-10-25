using MoneyRecord.Helpers;
using SQLite;

namespace MoneyRecord.Models
{
    /// <summary>
    /// A named part of an account that is set aside (Bank › Travel). Only an earmark: the account's balance never
    /// changes. Its currency is its account's. Its money is the sum of its <see cref="PocketMovement"/>s.
    /// </summary>
    public class Pocket : IEntity
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed(Name = "IX_Pocket_AccountId")]
        public int AccountId { get; set; }

        public string Name { get; set; } = string.Empty;

        /// <summary>Unicode code for a Material Design Icon (default: piggy bank).</summary>
        public string IconCode { get; set; } = "F1007";

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        [Ignore]
        public string DisplayIcon => IconHelper.GetAccountDisplayIcon(IconCode);
    }

    /// <summary>Money set aside in a pocket (positive) or taken out of it (negative).</summary>
    public class PocketMovement : IEntity
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed(Name = "IX_PocketMovement_PocketId")]
        public int PocketId { get; set; }

        /// <summary>Money value for view models; stored as <see cref="AmountCents"/>.</summary>
        [Ignore]
        public decimal Amount
        {
            get => Money.FromCents(AmountCents);
            set => AmountCents = Money.ToCents(value);
        }

        /// <summary>Signed value in cents (see <see cref="Money"/>): + set aside, − taken out.</summary>
        public long AmountCents { get; set; }

        public DateTime Date { get; set; }

        /// <summary>The expense this take-out covered, if any.</summary>
        public int? TransactionId { get; set; }

        /// <summary>The transfer this take-out covered, if any.</summary>
        public int? TransferId { get; set; }

        public string? Note { get; set; }
    }
}
