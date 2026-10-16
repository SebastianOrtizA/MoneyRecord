using MoneyRecord.Helpers;
using MoneyRecord.Models;

namespace MoneyRecord.Services.Interfaces
{
    /// <summary>
    /// The column choices of earlier imports, per bank: a bank's export always has the same header, so the header (or,
    /// without one, the column count) is the key.
    /// </summary>
    public interface IStatementMappingStore
    {
        /// <summary>
        /// The choices saved for this table's layout, if they still fit it (the date column still reads with the saved
        /// layout, the amount column is still one); null otherwise, and the import screen keeps the guess.
        /// </summary>
        SavedStatementMapping? Find(CsvTable table, StatementGuess guess);

        /// <summary>Saves the choices of a finished import for the next file with this layout.</summary>
        void Remember(CsvTable table, StatementMapping mapping, int accountId);
    }

    /// <summary>A saved mapping, ready to use: the columns and the account the last import of this layout went into.</summary>
    public sealed record SavedStatementMapping(StatementMapping Mapping, int AccountId);
}
