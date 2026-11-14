using MoneyRecord.Models;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace MoneyRecord.Helpers
{
    /// <summary>
    /// Reads the movements of an OFX / QFX statement (bank or credit card). Both flavours are handled by one tolerant tag
    /// reader: v1 is SGML (values end at the line end, most tags are never closed) and v2 is XML. A movement is a STMTTRN
    /// with TRNAMT (signed), DTPOSTED, FITID (the bank's own ID) and NAME / MEMO; the file's CURDEF is its currency.
    /// </summary>
    public static partial class OfxParser
    {
        [GeneratedRegex(@"<(/?)([A-Za-z0-9_.]+)>([^<]*)")]
        private static partial Regex Tag();

        public static ParsedStatement Read(Stream stream)
        {
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            return Parse(Decode(buffer.ToArray()));
        }

        public static bool LooksLikeOfx(string text) => text.Contains("<OFX", StringComparison.OrdinalIgnoreCase);

        public static ParsedStatement Parse(string text)
        {
            var entries = new List<StatementEntry>();
            string? currency = null;
            Dictionary<string, string>? current = null;

            void Finish()
            {
                if (current != null)
                    entries.Add(ToEntry(current, entries.Count + 1));
                current = null;
            }

            foreach (Match match in Tag().Matches(text))
            {
                var closing = match.Groups[1].Length > 0;
                var name = match.Groups[2].Value.ToUpperInvariant();
                var value = WebUtility.HtmlDecode(match.Groups[3].Value).Trim();

                if (name == "STMTTRN")
                {
                    Finish();
                    if (!closing)
                        current = [];
                }
                else if (closing)
                {
                    // End of the list (SGML never closes STMTTRN itself)
                    if (name is "BANKTRANLIST" or "STMTRS" or "CCSTMTRS")
                        Finish();
                }
                else if (name is "LEDGERBAL" or "AVAILBAL" or "BANKTRANLISTEND")
                {
                    Finish();
                }
                else if (current != null)
                {
                    current.TryAdd(name, value);
                }
                else if (name == "CURDEF" && currency == null && value.Length == 3)
                {
                    currency = value.ToUpperInvariant();
                }
            }
            Finish();
            return new ParsedStatement(entries, currency);
        }

        private static StatementEntry ToEntry(Dictionary<string, string> fields, int line)
        {
            fields.TryGetValue("NAME", out var name);
            fields.TryGetValue("MEMO", out var memo);
            var description = string.Join(" - ", new[] { name, memo }
                .Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase));

            DateTime? date = null;
            if (fields.TryGetValue("DTPOSTED", out var posted) && posted.Length >= 8
                && DateTime.TryParseExact(posted[..8], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                date = d;

            decimal? amount = null;
            if (fields.TryGetValue("TRNAMT", out var text))
            {
                // Some banks write a decimal comma
                var separator = text.Contains(',') && !text.Contains('.') ? ',' : '.';
                if (StatementAmount.TryParse(text, separator, out var parsed))
                    amount = parsed;
            }

            fields.TryGetValue("FITID", out var id);
            return new StatementEntry(line, date, description, amount, string.IsNullOrWhiteSpace(id) ? null : id);
        }

        private static string Decode(byte[] bytes)
        {
            var utf8 = new UTF8Encoding(false, throwOnInvalidBytes: true);
            try
            {
                return utf8.GetString(bytes);
            }
            catch (DecoderFallbackException)
            {
                // OFX 1.x headers say CHARSET:1252 (or ISO-8859-1): Latin-1 reads the letters both use
                return Encoding.Latin1.GetString(bytes);
            }
        }
    }
}
