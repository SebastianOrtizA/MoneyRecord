using MoneyRecord.Helpers;
using MoneyRecord.Models;

namespace MoneyRecord.Services.Interfaces
{
    /// <summary>Bank statement (CSV) import into one account: preview, import, undo.</summary>
    public interface IStatementImportService
    {
        /// <summary>
        /// The table's rows as they would be imported into <paramref name="account"/>. A row gets the category of the latest
        /// entry of its type with the same description (digits ignored: "SHOP 1234" = "SHOP 5678"), else the default for its
        /// type. Rows the account already has (same day, type, amount and description, counted: two equal coffees in the
        /// file and one recorded → one duplicate) and unreadable rows start excluded.
        /// </summary>
        Task<List<ImportRow>> PreviewAsync(CsvTable table, StatementMapping mapping, Account account,
            Category? expenseCategory, Category? incomeCategory);

        /// <summary>Adds the included rows that can be imported, in one database transaction; returns the new IDs.</summary>
        Task<List<int>> ImportAsync(Account account, IEnumerable<ImportRow> rows);

        /// <summary>Deletes the entries an import added, in one database transaction.</summary>
        Task UndoAsync(IReadOnlyCollection<int> transactionIds);
    }
}
