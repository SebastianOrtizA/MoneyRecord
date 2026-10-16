using MoneyRecord.Helpers;
using System.Globalization;
using System.Text;

namespace MoneyRecord.Models
{
    /// <summary>
    /// Which columns of a <see cref="CsvTable"/> hold the date, the description and the signed amount, and how to read them.
    /// <see cref="Guess"/> picks them from the header names (English / Spanish) or, failing that, from the values.
    /// </summary>
    public sealed record StatementMapping(
        int DateColumn,
        StatementDate DateFormat,
        int DescriptionColumn,
        int AmountColumn,
        char DecimalSeparator,
        bool PositiveIsExpense = false)
    {
        private static readonly string[] DateNames = ["date", "fecha", "posted", "booking", "valor fecha", "dia"];
        private static readonly string[] DescriptionNames = ["description", "descripcion", "concepto", "detalle", "details",
            "memo", "payee", "narrative", "referencia", "movimiento", "transaccion", "transaction"];
        private static readonly string[] AmountNames = ["amount", "importe", "monto", "valor", "value", "cantidad", "total"];

        /// <summary>
        /// A starting mapping plus the choices the import screen offers, all read from the table once (the layouts are
        /// tried on every value of every column, so a screen must not repeat it); null when the file has no date column
        /// or no amount column.
        /// </summary>
        public static StatementGuess? Guess(CsvTable table)
        {
            var dateFormats = Enumerable.Range(0, table.Headers.Count)
                .Select(c => (Column: c, Formats: StatementDate.Detect(table.Column(c))))
                .Where(c => c.Formats.Count > 0)
                .ToDictionary(c => c.Column, c => (IReadOnlyList<StatementDate>)c.Formats);
            var dates = dateFormats.Keys.Order().ToList();
            var amounts = Enumerable.Range(0, table.Headers.Count)
                .Where(c => !dateFormats.ContainsKey(c) && IsAmountColumn(table, c))
                .ToList();
            if (dates.Count == 0 || amounts.Count == 0)
                return null;

            var date = ByName(table, dates, DateNames) ?? dates[0];
            var amount = ByName(table, amounts, AmountNames) ?? amounts[0];
            var texts = Enumerable.Range(0, table.Headers.Count).Where(c => c != date && c != amount).ToList();
            var description = ByName(table, texts, DescriptionNames)
                // else the column with the longest texts
                ?? texts.OrderByDescending(c => table.Column(c).Sum(v => v.Length)).Select(c => (int?)c).FirstOrDefault()
                ?? amount;

            var mapping = new StatementMapping(date, dateFormats[date][0], description, 0, '.').WithAmountColumn(table, amount);
            return new StatementGuess(mapping, dateFormats, amounts);
        }

        /// <summary>This mapping with another amount column: the decimal separator follows that column's values.</summary>
        public StatementMapping WithAmountColumn(CsvTable table, int column) =>
            this with { AmountColumn = column, DecimalSeparator = StatementAmount.DetectDecimalSeparator(table.Column(column)) };

        private static bool IsAmountColumn(CsvTable table, int column)
        {
            var values = table.Column(column).Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
            if (values.Count == 0)
                return false;
            var separator = StatementAmount.DetectDecimalSeparator(values);
            return StatementAmount.IsMost(values.Count(v => StatementAmount.TryParse(v, separator, out _)), values.Count);
        }

        private static int? ByName(CsvTable table, List<int> columns, string[] names)
        {
            // An exact name beats one that only contains it ("Fecha valor" vs "Fecha")
            foreach (var exact in new[] { true, false })
            {
                foreach (var name in names)
                {
                    foreach (var column in columns)
                    {
                        var header = Normalize(table.Headers[column]);
                        if (exact ? header == name : header.Contains(name))
                            return column;
                    }
                }
            }
            return null;
        }

        /// <summary>Lower case, no accents: "Descripción" → "descripcion".</summary>
        internal static string Normalize(string text)
        {
            var decomposed = text.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
            return new string(decomposed.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
        }
    }

    /// <summary>
    /// What <see cref="StatementMapping.Guess"/> read from a table: the starting mapping, the date layouts that read each
    /// date column (best first) and the amount columns.
    /// </summary>
    public sealed record StatementGuess(
        StatementMapping Mapping,
        IReadOnlyDictionary<int, IReadOnlyList<StatementDate>> DateFormats,
        IReadOnlyList<int> AmountColumns);
}
