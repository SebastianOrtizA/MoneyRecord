using MoneyRecord.Models;
using System.Text;
using System.Text.RegularExpressions;

namespace MoneyRecord.Helpers
{
    /// <summary>
    /// A QIF file read as text: one record per movement, dates and amounts still as written. QIF dates have no fixed order
    /// (5/4/2026 is day-first or month-first), so the caller picks one of <see cref="DateFormats"/> and then calls
    /// <see cref="ToStatement"/>.
    /// </summary>
    public sealed class QifParser
    {
        private readonly List<Record> _records;

        private QifParser(List<Record> records) => _records = records;

        private sealed record Record(string Date, string Amount, string Payee, string Memo);

        public int Count => _records.Count;

        public static QifParser Read(Stream stream)
        {
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            return Parse(Decode(buffer.ToArray()));
        }

        /// <summary>Reads the bank, cash and credit-card sections; other sections (categories, investments, ...) are skipped.</summary>
        public static QifParser Parse(string text)
        {
            var records = new List<Record>();
            var reading = false;
            string date = "", amount = "", payee = "", memo = "";

            void Reset() => (date, amount, payee, memo) = ("", "", "", "");

            foreach (var raw in text.Split('\n'))
            {
                var line = raw.TrimEnd('\r', ' ');
                if (line.Length == 0)
                    continue;

                if (line[0] == '!')
                {
                    // !Type:Bank, !Type:CCard, !Type:Cash, !Type:Oth A, !Type:Oth L; !Option and !Account lines change nothing
                    if (line.StartsWith("!Type:", StringComparison.OrdinalIgnoreCase))
                    {
                        var kind = line[6..].Trim().ToLowerInvariant();
                        reading = kind is "bank" or "ccard" or "cash" or "oth a" or "oth l";
                        Reset();
                    }
                    continue;
                }
                if (!reading)
                    continue;

                var value = line[1..].Trim();
                switch (line[0])
                {
                    case 'D': date = value; break;
                    case 'T' or 'U': if (amount.Length == 0) amount = value; break;
                    case 'P': payee = value; break;
                    case 'M': memo = value; break;
                    case '^':
                        if (date.Length > 0 || amount.Length > 0)
                            records.Add(new Record(date, amount, payee, memo));
                        Reset();
                        break;
                }
            }
            return new QifParser(records);
        }

        /// <summary>The layouts that read the file's dates, the likeliest first (empty when none does).</summary>
        public List<StatementDate> DateFormats => StatementDate.Detect(_records.Select(r => NormalizeDate(r.Date)));

        public ParsedStatement ToStatement(StatementDate format)
        {
            var separator = StatementAmount.DetectDecimalSeparator(_records.Select(r => r.Amount));
            var entries = _records.Select((r, i) =>
            {
                DateTime? date = format.TryParse(NormalizeDate(r.Date), out var d) ? d : null;
                decimal? amount = StatementAmount.TryParse(r.Amount, separator, out var a) ? a : null;
                var description = string.Join(" - ", new[] { r.Payee, r.Memo }
                    .Where(s => s.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase));
                return new StatementEntry(i + 1, date, description, amount);
            }).ToList();
            return new ParsedStatement(entries);
        }

        /// <summary>"5/ 4'26" (Quicken: a space pads a day, an apostrophe starts a year after 1999) → "5/4/26".</summary>
        internal static string NormalizeDate(string date) =>
            Regex.Replace(date.Replace(" ", ""), @"'(\d\d)$", "/20$1").Replace('\'', '/');

        private static string Decode(byte[] bytes)
        {
            try
            {
                return new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes);
            }
            catch (DecoderFallbackException)
            {
                return Encoding.Latin1.GetString(bytes);
            }
        }
    }
}
