using CsvHelper;
using CsvHelper.Configuration;
using MoneyRecord.Resources.Strings;
using System.Globalization;
using System.Text;

namespace MoneyRecord.Helpers
{
    /// <summary>
    /// A bank statement read from a CSV file: the header and the data rows, every row with <see cref="Headers"/>' width.
    /// <para>
    /// Banks add lines around the table (account number, opening balance, totals), so the table is the longest run of rows
    /// with the most common width; its first row is the header, unless it already looks like data (a date and a number),
    /// then the columns are named "Column 1", "Column 2", … The delimiter (, ; tab |) is detected, and a file that isn't
    /// valid UTF-8 is read as Latin-1 (the usual export of older bank sites).
    /// </para>
    /// </summary>
    public sealed class CsvTable
    {
        private CsvTable(IReadOnlyList<string> headers, IReadOnlyList<string[]> rows)
        {
            Headers = headers;
            Rows = rows;
        }

        public IReadOnlyList<string> Headers { get; }

        public IReadOnlyList<string[]> Rows { get; }

        /// <summary>Every row's value in <paramref name="column"/>.</summary>
        public IEnumerable<string> Column(int column) => Rows.Select(r => r[column]);

        public static CsvTable Read(Stream stream)
        {
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            return Parse(Decode(buffer.ToArray()));
        }

        public static CsvTable Parse(string text)
        {
            // The delimiter whose table holds the most cells. Not CsvHelper's detection: it counts characters, and the
            // decimal commas of "-45.000,00" outnumber the semicolons that separate the columns
            var table = Delimiters
                .Select(d => FindTable(ReadRecords(text, d)))
                .MaxBy(t => t.Count * (t.FirstOrDefault()?.Length ?? 0))!;
            if (table.Count == 0)
                return new CsvTable([], []);

            var first = table[0];
            var hasHeader = !LooksLikeData(first);
            var headers = hasHeader
                ? first.Select((h, i) => string.IsNullOrWhiteSpace(h) ? ColumnName(i) : h.Trim()).ToList()
                : first.Select((_, i) => ColumnName(i)).ToList();
            return new CsvTable(headers, hasHeader ? table.Skip(1).ToList() : table);
        }

        private static string ColumnName(int index) => string.Format(AppResources.ImportColumnName, index + 1);

        private static string Decode(byte[] bytes)
        {
            try
            {
                // Strict: invalid bytes throw instead of becoming "�"
                var text = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes);
                return text.TrimStart('﻿');
            }
            catch (DecoderFallbackException)
            {
                return Encoding.Latin1.GetString(bytes);
            }
        }

        private static readonly string[] Delimiters = [",", ";", "\t", "|"];

        private static List<string[]> ReadRecords(string text, string delimiter)
        {
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = false,
                Delimiter = delimiter,
                BadDataFound = null,
                MissingFieldFound = null,
                TrimOptions = TrimOptions.Trim,
                IgnoreBlankLines = true,
            };
            using var reader = new StringReader(text);
            using var csv = new CsvParser(reader, config);
            var records = new List<string[]>();
            while (csv.Read())
            {
                var record = csv.Record;
                if (record != null && record.Any(v => !string.IsNullOrWhiteSpace(v)))
                    records.Add(record);
            }
            return records;
        }

        /// <summary>The longest run of consecutive rows with the most common width (at least two columns).</summary>
        private static List<string[]> FindTable(List<string[]> records)
        {
            var width = records
                .Where(r => r.Length >= 2)
                .GroupBy(r => r.Length)
                .OrderByDescending(g => g.Count())
                .ThenByDescending(g => g.Key)
                .Select(g => g.Key)
                .FirstOrDefault();
            if (width == 0)
                return [];

            List<string[]> best = [], current = [];
            foreach (var record in records)
            {
                if (record.Length == width)
                {
                    current.Add(record);
                    if (current.Count > best.Count)
                        best = current;
                }
                else
                {
                    current = [];
                }
            }
            return best;
        }

        private static bool LooksLikeData(string[] row) =>
            row.Any(v => StatementDate.Formats.Any(f => f.TryParse(v, out _)))
            && row.Any(v => StatementAmount.TryParse(v, '.', out _) || StatementAmount.TryParse(v, ',', out _));
    }
}
