using MoneyRecord.Helpers;
using SQLite;

namespace MoneyRecord.Models
{
    public class Transfer : IEntity
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed(Name = "IX_Transfer_Date")]
        public DateTime Date { get; set; }

        /// <summary>
        /// Money value for view models and JSON v1 backups; stored as <see cref="AmountCents"/>.
        /// </summary>
        [Ignore]
        public decimal Amount
        {
            get => Money.FromCents(AmountCents);
            set => AmountCents = Money.ToCents(value);
        }

        /// <summary>
        /// Stored value in cents (see <see cref="Money"/>). Declared after <see cref="Amount"/>, so a JSON backup writes it
        /// later in the file; on import System.Text.Json applies values in file order, so the exact value wins.
        /// </summary>
        public long AmountCents { get; set; }

        /// <summary>
        /// What arrived in the destination account, in its currency; stored as <see cref="DestinationAmountCents"/>.
        /// Equal to <see cref="Amount"/> when both accounts have the same currency.
        /// </summary>
        [Ignore]
        public decimal DestinationAmount
        {
            get => Money.FromCents(DestinationAmountCents);
            set => DestinationAmountCents = Money.ToCents(value);
        }

        /// <summary>Stored value in cents; declared after <see cref="DestinationAmount"/> for the same reason as <see cref="AmountCents"/>.</summary>
        public long DestinationAmountCents { get; set; }

        [Indexed(Name = "IX_Transfer_SourceAccountId")]
        public int SourceAccountId { get; set; }

        [Indexed(Name = "IX_Transfer_DestinationAccountId")]
        public int DestinationAccountId { get; set; }

        public string Description { get; set; } = string.Empty;

        [Ignore]
        public string SourceAccountName { get; set; } = string.Empty;

        [Ignore]
        public string DestinationAccountName { get; set; } = string.Empty;

        /// <summary>The source account's currency (empty = the base currency), filled in for display.</summary>
        [Ignore]
        public string CurrencyCode { get; set; } = string.Empty;

        /// <summary>The destination account's currency (empty = the base currency), filled in for display.</summary>
        [Ignore]
        public string DestinationCurrencyCode { get; set; } = string.Empty;

        /// <summary>True when the two accounts have different currencies (filled-in codes; empty = the base currency).</summary>
        [Ignore]
        public bool IsCrossCurrency => !string.Equals(
            MoneyFormatter.ResolveCurrencyCode(CurrencyCode),
            MoneyFormatter.ResolveCurrencyCode(DestinationCurrencyCode),
            StringComparison.OrdinalIgnoreCase);
    }
}
