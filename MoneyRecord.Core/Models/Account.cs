using MoneyRecord.Helpers;
using SQLite;

namespace MoneyRecord.Models
{
    public class Account : IEntity
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Money value for view models and JSON v1 backups; stored as <see cref="InitialBalanceCents"/>.
        /// </summary>
        [Ignore]
        public decimal InitialBalance
        {
            get => Money.FromCents(InitialBalanceCents);
            set => InitialBalanceCents = Money.ToCents(value);
        }

        /// <summary>
        /// Stored value in cents (see <see cref="Money"/>). Declared after <see cref="InitialBalance"/>, so a JSON backup writes it
        /// later in the file; on import System.Text.Json applies values in file order, so the exact value wins.
        /// </summary>
        public long InitialBalanceCents { get; set; }

        public bool IsDefault { get; set; }

        /// <summary>
        /// Unicode code for Material Design Icon (e.g., "F0070" for bank icon)
        /// </summary>
        public string IconCode { get; set; } = "F0070"; // Default: bank icon

        /// <summary>The icon of the Cash account a new database starts with.</summary>
        public const string CashIconCode = "F0115";

        /// <summary>
        /// The date when the account was created or when the initial balance was assigned.
        /// Used as fallback for last activity date when no transactions or transfers exist.
        /// </summary>
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        /// <summary>
        /// Indicates whether the account allows negative balance.
        /// When false, users cannot add expenses that would result in a negative balance.
        /// Default is false (negative balance not allowed).
        /// </summary>
        public bool AllowNegativeBalance { get; set; } = false;

        public string CurrencyCode { get; set; } = string.Empty;

        /// <summary>
        /// The hidden account of a <see cref="Debt"/>: its balance is what the person owes (lent, positive) or is owed
        /// (borrowed, negative), moved by transfers from and to real accounts. Never listed among the user's accounts or pickers.
        /// </summary>
        public bool IsDebt { get; set; }

        [Ignore]
        public string DisplayIcon => IconHelper.GetAccountDisplayIcon(IconCode);
    }

    /// <summary>
    /// Represents an icon option for account selection
    /// </summary>
    public class AccountIcon : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public string DisplayIcon => IconHelper.GetAccountDisplayIcon(Code);
    }
}
