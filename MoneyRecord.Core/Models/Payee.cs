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
    public sealed record PayeeSummary(int Id, string Name, int TransactionCount, DateTime? LastUsed)
    {
        /// <summary>"12 transactions · last Oct 3, 2026"; "0 transactions" for an unused payee.</summary>
        public string UsageText => LastUsed is { } date
            ? $"{Plurals.Transactions(TransactionCount)} · {string.Format(AppResources.PayeeLastUsed, DateFormatter.Format(date, DateStyle.Medium))}"
            : Plurals.Transactions(TransactionCount);
    }
}
