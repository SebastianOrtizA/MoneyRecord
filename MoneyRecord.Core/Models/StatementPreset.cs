using MoneyRecord.Helpers;

namespace MoneyRecord.Models
{
    /// <summary>A competitor app's export recognised by its header, with the mapping that reads it.</summary>
    public sealed record StatementPreset(string Name, StatementMapping Mapping);

    /// <summary>
    /// Exports of other finance apps, told apart by their header names (not by values): YNAB's register export (Outflow and
    /// Inflow columns) and Mint's transactions export (unsigned Amount plus Transaction Type). <see cref="StatementMapping.Guess"/>
    /// stays the fallback for every other file.
    /// </summary>
    public static class StatementPresets
    {
        public static StatementPreset? Detect(CsvTable table, StatementGuess guess)
        {
            if (!table.HasHeader)
                return null;
            var headers = table.Headers.Select(StatementMapping.Normalize).ToList();
            int? Find(params string[] names) => names.Select(n => headers.IndexOf(n)).Where(i => i >= 0).Select(i => (int?)i).FirstOrDefault();

            if (Find("date") is not { } date || !guess.DateFormats.ContainsKey(date))
                return null;

            // YNAB: Account, Flag, Date, Payee, Category, Memo, Outflow, Inflow, Cleared (nYNAB adds "Category Group/Category")
            if (Find("payee") is { } payee && Find("outflow") is { } outflow && Find("inflow") is { } inflow)
            {
                var separator = StatementAmount.DetectDecimalSeparator(table.Column(outflow).Concat(table.Column(inflow)));
                var mapping = new StatementMapping(date, DateFormat(guess, date), payee, outflow, separator,
                    CategoryColumn: Find("category"), PayeeColumn: payee, InflowColumn: inflow);
                return new StatementPreset("YNAB", mapping);
            }

            // Mint: Date, Description, Original Description, Amount, Transaction Type, Category, Account Name, Labels, Notes
            if (Find("description") is { } description && Find("amount") is { } amount && Find("transaction type") is { } type)
            {
                var separator = StatementAmount.DetectDecimalSeparator(table.Column(amount));
                var mapping = new StatementMapping(date, DateFormat(guess, date), description, amount, separator,
                    CategoryColumn: Find("category"), PayeeColumn: description, TypeColumn: type);
                return new StatementPreset("Mint", mapping);
            }

            return null;
        }

        // Both apps write US dates; a file whose dates all fit day-first and month-first is month-first
        private static StatementDate DateFormat(StatementGuess guess, int column)
        {
            var formats = guess.DateFormats[column];
            return formats.FirstOrDefault(f => f.Pattern is "M/d/yyyy" or "M/d/yy") ?? formats[0];
        }
    }
}
