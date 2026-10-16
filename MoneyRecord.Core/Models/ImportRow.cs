using CommunityToolkit.Mvvm.ComponentModel;
using MoneyRecord.Helpers;
using MoneyRecord.Resources.Strings;

namespace MoneyRecord.Models
{
    /// <summary>Why a statement row can't be imported.</summary>
    public enum ImportProblem
    {
        None,
        Date,
        Amount,
        ZeroAmount,
    }

    /// <summary>
    /// One statement row in the import preview: what it would become, whether it looks already recorded, and the user's
    /// choices (import it or not, its category).
    /// </summary>
    public sealed partial class ImportRow : ObservableObject
    {
        /// <summary>The row's position in the file's table (1 = the first data row).</summary>
        public int Line { get; init; }

        public DateTime Date { get; init; }

        public string Description { get; init; } = string.Empty;

        /// <summary>Unsigned, in the account's currency (see <see cref="Money"/>).</summary>
        public long AmountCents { get; init; }

        public decimal Amount => Money.FromCents(AmountCents);

        public TransactionType Type { get; init; }

        public string CurrencyCode { get; init; } = string.Empty;

        public ImportProblem Problem { get; init; }

        /// <summary>The account already has an entry with the same day, type, amount and description.</summary>
        public bool IsDuplicate { get; init; }

        /// <summary>The category came from an earlier entry with the same description, not the import screen's default.</summary>
        public bool IsLearned { get; init; }

        public bool CanImport => Problem == ImportProblem.None;

        /// <summary>Negative for an expense, in the account's currency; empty for a row that can't be imported.</summary>
        public string AmountText => CanImport
            ? MoneyFormatter.Format(Type == TransactionType.Expense ? -Amount : Amount, CurrencyCode)
            : string.Empty;

        public string DateText => Problem == ImportProblem.Date ? string.Empty : DateFormatter.Format(Date, DateStyle.Medium);

        /// <summary>Why the row starts excluded ("Already recorded", "Unreadable date", …); empty otherwise.</summary>
        public string StatusText => Problem switch
        {
            ImportProblem.Date => AppResources.ImportProblemDate,
            ImportProblem.Amount => AppResources.ImportProblemAmount,
            ImportProblem.ZeroAmount => AppResources.ImportProblemZero,
            _ => IsDuplicate ? AppResources.ImportAlreadyRecorded : string.Empty,
        };

        public bool HasStatus => StatusText.Length > 0;

        [ObservableProperty]
        private bool isIncluded;

        [ObservableProperty]
        private Category? category;
    }
}
