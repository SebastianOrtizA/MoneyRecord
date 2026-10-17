using MoneyRecord.Helpers;
using MoneyRecord.Resources.Strings;
using SQLite;

namespace MoneyRecord.Models
{
    /// <summary>
    /// Who an income or expense was with (a shop, an employer). Optional on a transaction (<see cref="Transaction.PayeeId"/>);
    /// created by typing a new name in the form. Names are unique ignoring case (<see cref="PayeeNames"/>).
    /// </summary>
    public class Payee : IEntity
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    /// <summary>A payee with how often and how recently it was used: the form's suggestions and the Payees page.</summary>
    /// <remarks><paramref name="RecurringCount"/> = the recurring transactions that give it to each occurrence.</remarks>
    public sealed record PayeeSummary(int Id, string Name, int TransactionCount, DateTime? LastUsed, int RecurringCount = 0,
        int DebtCount = 0)
    {
        /// <summary>
        /// What uses it: "2 transactions", or "0 transactions, 1 recurring, 1 debt" with recurring transactions or debts.
        /// </summary>
        public string UsesText => string.Join(", ", new[]
        {
            Plurals.Transactions(TransactionCount),
            RecurringCount > 0 ? Plurals.Recurring(RecurringCount) : null,
            DebtCount > 0 ? Plurals.Debts(DebtCount) : null
        }.OfType<string>());

        /// <summary>The Payees page line: "12 transactions · last Oct 3, 2026"; <see cref="UsesText"/> alone when never used.</summary>
        public string UsageText => LastUsed is { } date
            ? $"{UsesText} · {string.Format(AppResources.PayeeLastUsed, DateFormatter.Format(date, DateStyle.Medium))}"
            : UsesText;
    }
}
