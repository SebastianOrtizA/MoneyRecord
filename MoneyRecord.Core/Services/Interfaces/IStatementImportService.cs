using MoneyRecord.Helpers;
using MoneyRecord.Models;

namespace MoneyRecord.Services.Interfaces
{
    /// <summary>Bank statement (CSV, OFX, QIF) import into one account: preview, import, undo.</summary>
    public interface IStatementImportService
    {
        /// <summary>
        /// The table's rows as they would be imported into <paramref name="account"/>. A row gets the category of the latest
        /// entry of its type with the same description (digits ignored: "SHOP 1234" = "SHOP 5678"), else the default for its
        /// type. Rows the account already has (same day, type, amount and description, counted: two equal coffees in the
        /// file and one recorded → one duplicate) and unreadable rows start excluded.
        /// </summary>
        /// <remarks>
        /// A category the file names (<see cref="StatementMapping.CategoryColumn"/>) is matched by name to the account's
        /// categories of that type and beats the learned one; with <paramref name="createMissingCategories"/> an unknown name
        /// becomes a new category when the rows are imported. A named payee is found or created by name.
        /// </remarks>
        Task<List<ImportRow>> PreviewAsync(CsvTable table, StatementMapping mapping, Account account,
            Category? expenseCategory, Category? incomeCategory, bool createMissingCategories = false);

        /// <summary>The same for an OFX or QIF file: a file in another currency than the account is all problems; a row with a bank ID is a duplicate when that ID is already recorded.</summary>
        Task<List<ImportRow>> PreviewAsync(ParsedStatement statement, Account account, Category? expenseCategory, Category? incomeCategory,
            bool createMissingCategories = false);

        /// <summary>Adds the included rows that can be imported, in one database transaction; returns the new IDs.</summary>
        Task<List<int>> ImportAsync(Account account, IEnumerable<ImportRow> rows);

        /// <summary>Deletes the entries an import added, in one database transaction.</summary>
        Task UndoAsync(IReadOnlyCollection<int> transactionIds);
    }
}
