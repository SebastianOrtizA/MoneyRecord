using MoneyRecord.Helpers;
using SQLite;

namespace MoneyRecord.Models
{
    public class Transaction : IEntity
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed(Name = "IX_Transaction_Date")]
        public DateTime Date { get; set; }

        public string Description { get; set; } = string.Empty;

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

        [Indexed(Name = "IX_Transaction_CategoryId")]
        public int CategoryId { get; set; }

        public TransactionType Type { get; set; }

        [Indexed(Name = "IX_Transaction_AccountId")]
        public int? AccountId { get; set; }

        /// <summary>Who the money went to or came from (see <see cref="Payee"/>); none = no payee.</summary>
        [Indexed(Name = "IX_Transaction_PayeeId")]
        public int? PayeeId { get; set; }

        /// <summary>
        /// Shared by the parts of one payment split across categories: each part is a normal transaction (its own category and
        /// amount), so every total counts it; they are edited and deleted together. Null = not split.
        /// </summary>
        [Indexed(Name = "IX_Transaction_SplitId")]
        public string? SplitId { get; set; }

        [Ignore]
        public bool IsSplit => !string.IsNullOrEmpty(SplitId);

        public string? Tags { get; set; }

        public string? ReceiptPhotoPath { get; set; }

        /// <summary>
        /// The user's own conversion (the "Amount in USD" a bank statement shows), in <see cref="ConvertedCurrencyCode"/>;
        /// null = convert with the downloaded rate. Stored as <see cref="ConvertedAmountCents"/>.
        /// </summary>
        [Ignore]
        public decimal? ConvertedAmount
        {
            get => ConvertedAmountCents is { } cents ? Money.FromCents(cents) : null;
            set => ConvertedAmountCents = value is { } amount ? Money.ToCents(amount) : null;
        }

        /// <summary>Stored value in cents; declared after <see cref="ConvertedAmount"/> for the same reason as <see cref="AmountCents"/>.</summary>
        public long? ConvertedAmountCents { get; set; }

        /// <summary>
        /// The currency of <see cref="ConvertedAmount"/> (the base currency when it was entered). A conversion into another
        /// currency (the base changed later) is ignored, never misread.
        /// </summary>
        public string? ConvertedCurrencyCode { get; set; }

        /// <summary>A copy in <paramref name="currencyCode"/> with <paramref name="amount"/> (unsigned): a converted entry for a report.</summary>
        public Transaction WithAmount(decimal amount, string currencyCode)
        {
            var copy = (Transaction)MemberwiseClone();
            copy.Amount = Math.Abs(amount);
            copy.CurrencyCode = currencyCode;
            copy.ConvertedAmount = null;
            copy.ConvertedCurrencyCode = null;
            return copy;
        }

        /// <summary>
        /// Applies the form's "Amount in {base}" field: a positive amount is the conversion into the base currency; an empty
        /// field clears a conversion into the base but keeps one into another currency (the field didn't show it). An
        /// account already in the base currency needs none.
        /// </summary>
        public void SetConversion(decimal? amountInBase, string? accountCurrency, string baseCurrency)
        {
            if (MoneyFormatter.SameCurrency(accountCurrency, baseCurrency))
            {
                ConvertedAmount = null;
                ConvertedCurrencyCode = null;
            }
            else if (amountInBase is > 0 and var amount)
            {
                ConvertedAmount = amount;
                ConvertedCurrencyCode = MoneyFormatter.ResolveCurrencyCode(baseCurrency);
            }
            else if (MoneyFormatter.SameCurrency(ConvertedCurrencyCode, baseCurrency) && ConvertedCurrencyCode != null)
            {
                ConvertedAmount = null;
                ConvertedCurrencyCode = null;
            }
        }

        [Ignore]
        public List<string> TagList => string.IsNullOrWhiteSpace(Tags)
            ? new List<string>()
            : Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        [Ignore]
        public bool HasTags => !string.IsNullOrWhiteSpace(Tags);

        [Ignore]
        public bool HasReceipt => !string.IsNullOrWhiteSpace(ReceiptPhotoPath);

        [Ignore]
        public string CategoryName { get; set; } = string.Empty;

        [Ignore]
        public string AccountName { get; set; } = string.Empty;

        /// <summary>The payee's name (empty = none), filled in when the transaction is loaded for display.</summary>
        [Ignore]
        public string PayeeName { get; set; } = string.Empty;

        /// <summary>The account's currency (empty = the base currency), filled in when the transaction is loaded for display.</summary>
        [Ignore]
        public string CurrencyCode { get; set; } = string.Empty;

        /// <summary>
        /// Icon code for the category (Material Design Icons)
        /// </summary>
        [Ignore]
        public string CategoryIconCode { get; set; } = "F0770";

        /// <summary>
        /// Gets the displayable icon character from the category unicode code
        /// </summary>
        [Ignore]
        public string CategoryDisplayIcon => IconHelper.GetCategoryDisplayIcon(CategoryIconCode);

        /// <summary>
        /// Icon code for the account (Material Design Icons)
        /// </summary>
        [Ignore]
        public string AccountIconCode { get; set; } = "F0070";

        /// <summary>
        /// Gets the displayable icon character from the account unicode code
        /// </summary>
        [Ignore]
        public string AccountDisplayIcon => IconHelper.GetAccountDisplayIcon(AccountIconCode);
    }

    public enum TransactionType
    {
        Income,
        Expense,
        Transfer
    }
}
