using MoneyRecord.Helpers;

namespace MoneyRecord.Models
{
    public enum LedgerEntryKind
    {
        Income,
        Expense,
        /// <summary>A whole transfer, one row (source → destination).</summary>
        Transfer,
        /// <summary>The source account's half of a transfer (shown per account).</summary>
        TransferOut,
        /// <summary>The destination account's half of a transfer (shown per account).</summary>
        TransferIn
    }

    /// <summary>
    /// One row of the Home list: a transaction or a transfer (or half of one), with names and icons filled in.
    /// <see cref="SourceId"/> is the <see cref="Transaction"/> ID, or the <see cref="Transfer"/> ID for the transfer kinds.
    /// </summary>
    public sealed record LedgerEntry
    {
        public required LedgerEntryKind Kind { get; init; }

        public required int SourceId { get; init; }

        public DateTime Date { get; init; }

        public string Description { get; init; } = string.Empty;

        /// <summary>A transfer's own description (empty when it has none); <see cref="Description"/> adds the account names.</summary>
        public string TransferNote { get; init; } = string.Empty;

        /// <summary>The stored amount in cents (see <see cref="Money"/>); <see cref="SignedAmount"/> carries the direction.</summary>
        public long AmountCents { get; init; }

        /// <summary>The account's currency (empty = the base currency); a transfer uses its source account's.</summary>
        public string CurrencyCode { get; init; } = string.Empty;

        /// <summary>Null for transfers, which use the localized "Transfers" category name.</summary>
        public int? CategoryId { get; init; }

        public string CategoryName { get; init; } = string.Empty;

        public string CategoryIconCode { get; init; } = IconHelper.DefaultCategoryIconCode;

        /// <summary>The entry's account: a transfer's source, or the account of a transfer half.</summary>
        public int? AccountId { get; init; }

        public string AccountName { get; init; } = string.Empty;

        public string AccountIconCode { get; init; } = IconHelper.DefaultAccountIconCode;

        /// <summary>The other account of a transfer (its destination, or the source of a <see cref="LedgerEntryKind.TransferIn"/>).</summary>
        public int? CounterpartAccountId { get; init; }

        public string CounterpartAccountName { get; init; } = string.Empty;

        /// <summary>
        /// A transfer's amount on the counterpart side, in <see cref="CounterpartCurrencyCode"/>: what arrived for a
        /// whole transfer, what left for a <see cref="LedgerEntryKind.TransferIn"/>. 0 for transactions.
        /// </summary>
        public long CounterpartAmountCents { get; init; }

        /// <summary>The counterpart account's currency (empty = the base currency).</summary>
        public string CounterpartCurrencyCode { get; init; } = string.Empty;

        public string? Tags { get; init; }

        public string? ReceiptPhotoPath { get; init; }

        public decimal Amount => Money.FromCents(AmountCents);

        /// <summary>Negative for expenses and money leaving an account; a whole transfer is shown as positive.</summary>
        public decimal SignedAmount => Kind is LedgerEntryKind.Expense or LedgerEntryKind.TransferOut
            ? -Math.Abs(Amount)
            : Math.Abs(Amount);

        public bool IsTransfer => Kind is LedgerEntryKind.Transfer or LedgerEntryKind.TransferOut or LedgerEntryKind.TransferIn;

        /// <summary>A transfer between accounts of different currencies.</summary>
        public bool IsCrossCurrency => IsTransfer && !string.Equals(
            MoneyFormatter.ResolveCurrencyCode(CurrencyCode),
            MoneyFormatter.ResolveCurrencyCode(CounterpartCurrencyCode),
            StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// The signed amount in the entry's currency; a whole cross-currency transfer also shows what arrived
        /// on a second line ("$400,000" over "→ US$100.00").
        /// </summary>
        public string AmountText => Kind == LedgerEntryKind.Transfer && IsCrossCurrency
            ? $"{MoneyFormatter.Format(SignedAmount, CurrencyCode)}\n→ " +
              MoneyFormatter.Format(Math.Abs(Money.FromCents(CounterpartAmountCents)), CounterpartCurrencyCode)
            : MoneyFormatter.Format(SignedAmount, CurrencyCode);

        public TransactionType Type => Kind switch
        {
            LedgerEntryKind.Income => TransactionType.Income,
            LedgerEntryKind.Expense => TransactionType.Expense,
            _ => TransactionType.Transfer
        };

        /// <summary>"Checking → Savings" for a whole transfer, otherwise the account name.</summary>
        public string AccountDisplayName => Kind == LedgerEntryKind.Transfer
            ? $"{AccountName} → {CounterpartAccountName}"
            : AccountName;

        public bool HasTags => !string.IsNullOrWhiteSpace(Tags);

        /// <summary>The comma-separated <see cref="Tags"/>, trimmed.</summary>
        public IReadOnlyList<string> TagList => string.IsNullOrWhiteSpace(Tags)
            ? []
            : Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        public bool HasReceipt => !string.IsNullOrWhiteSpace(ReceiptPhotoPath);

        public string CategoryDisplayIcon => IconHelper.GetCategoryDisplayIcon(CategoryIconCode);

        public string AccountDisplayIcon => IconHelper.GetAccountDisplayIcon(AccountIconCode);
    }
}
