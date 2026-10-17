using PdfSharp.Fonts;
using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace MoneyRecord.Services.Pdf
{
    /// <summary>
    /// The PDF report's fonts: the app's Open Sans (Regular, Semibold for bold), embedded in Core. PDFsharp's platform-
    /// independent build has no system fonts, so every font comes from here; italic is simulated. Open Sans has no
    /// arrows, emoji or several currency signs, so texts go through <see cref="Printable"/> first.
    /// </summary>
    public sealed class PdfFonts : IFontResolver
    {
        public const string Family = "Open Sans";

        private const string Regular = "OpenSans-Regular";
        private const string Semibold = "OpenSans-Semibold";

        private static readonly object Gate = new();

        // The characters Open Sans can draw (both weights cover the same ones)
        private static readonly Lazy<HashSet<int>> Covered = new(() => ReadCharacterMap(Load(Regular)!));

        // A sign the font lacks → its ISO code, when only one currency uses it ("₹" → INR; "$" is never needed)
        private static readonly Lazy<Dictionary<string, string>> SymbolCodes = new(() => CultureInfo.GetCultures(CultureTypes.SpecificCultures)
            .Select(c => { try { return new RegionInfo(c.Name); } catch (ArgumentException) { return null; } })
            .Where(r => r != null && r.CurrencySymbol.Length > 0)
            .GroupBy(r => r!.CurrencySymbol)
            .Where(g => g.Select(r => r!.ISOCurrencySymbol).Distinct().Count() == 1)
            .ToDictionary(g => g.Key, g => g.First()!.ISOCurrencySymbol));

        /// <summary>Installs the fonts once (PDFsharp allows a single resolver per process).</summary>
        public static void Install()
        {
            lock (Gate)
            {
                if (GlobalFontSettings.FontResolver is not PdfFonts)
                    GlobalFontSettings.FontResolver = new PdfFonts();
            }
        }

        // Any family asked for gets Open Sans: MigraDoc's defaults never reach the system
        public FontResolverInfo ResolveTypeface(string familyName, bool bold, bool italic) =>
            new(bold ? Semibold : Regular, mustSimulateBold: false, mustSimulateItalic: italic);

        public byte[]? GetFont(string faceName) => Load(faceName);

        /// <summary>Whether the fonts have a glyph for the character.</summary>
        public static bool CanShow(int codePoint) => Covered.Value.Contains(codePoint);

        /// <summary>
        /// The text with what the font can't draw replaced: "▲ 12%" → "+12%", "▼" → "−", "→" → "›", a thin or narrow
        /// space → a no-break space, a currency sign → its ISO code ("₹1,200.00" → "INR 1,200.00"); anything else
        /// missing (emoji) is left out rather than drawn as an empty box.
        /// </summary>
        public static string Printable(string text)
        {
            if (text.EnumerateRunes().All(r => CanShow(r.Value)))
                return text;

            text = text.Replace("▲ ", "+").Replace("▼ ", "−");
            var result = new StringBuilder(text.Length + 8);
            var runes = text.EnumerateRunes().ToList();
            for (var i = 0; i < runes.Count; i++)
            {
                var rune = runes[i];
                if (CanShow(rune.Value))
                {
                    result.Append(rune.ToString());
                    continue;
                }

                switch (rune.Value)
                {
                    case '▲':
                        result.Append('+');
                        continue;
                    case '▼':
                        result.Append('−');
                        continue;
                    case '→':
                        result.Append('›');
                        continue;
                    case '\u2009' or '\u202F':
                        result.Append('\u00A0');
                        continue;
                }

                if (Rune.GetUnicodeCategory(rune) == UnicodeCategory.CurrencySymbol
                    && SymbolCodes.Value.TryGetValue(rune.ToString(), out var code))
                {
                    // Keep the code apart from the digits it touches
                    var before = i > 0 && Rune.IsDigit(runes[i - 1]) ? "\u00A0" : string.Empty;
                    var after = i + 1 < runes.Count && (Rune.IsDigit(runes[i + 1]) || runes[i + 1].Value == '-') ? "\u00A0" : string.Empty;
                    result.Append(before).Append(code).Append(after);
                }
            }

            return result.ToString();
        }

        private static byte[]? Load(string faceName)
        {
            using var stream = typeof(PdfFonts).Assembly.GetManifestResourceStream($"MoneyRecord.Fonts.{faceName}.ttf");
            if (stream == null)
                return null;
            using var bytes = new MemoryStream();
            stream.CopyTo(bytes);
            return bytes.ToArray();
        }

        /// <summary>The code points a TrueType font maps to a glyph (its Windows Unicode BMP cmap, format 4).</summary>
        internal static HashSet<int> ReadCharacterMap(byte[] font)
        {
            ushort U16(int at) => BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(at));
            uint U32(int at) => BinaryPrimitives.ReadUInt32BigEndian(font.AsSpan(at));

            var cmap = Enumerable.Range(0, U16(4)).Select(i => 12 + 16 * i)
                .Where(record => Encoding.ASCII.GetString(font, record, 4) == "cmap")
                .Select(record => (int)U32(record + 8))
                .Single();
            var subtable = Enumerable.Range(0, U16(cmap + 2)).Select(i => cmap + 4 + 8 * i)
                .Where(record => U16(record) == 3 && U16(record + 2) == 1)
                .Select(record => cmap + (int)U32(record + 4))
                .Single();

            var covered = new HashSet<int>();
            var segments = U16(subtable + 6) / 2;
            var ends = subtable + 14;
            var starts = ends + 2 * segments + 2;
            var deltas = starts + 2 * segments;
            var rangeOffsets = deltas + 2 * segments;
            for (var s = 0; s < segments; s++)
            {
                int start = U16(starts + 2 * s), end = U16(ends + 2 * s), delta = U16(deltas + 2 * s), rangeOffset = U16(rangeOffsets + 2 * s);
                for (var c = start; c <= end && c != 0xFFFF; c++)
                {
                    var glyph = rangeOffset == 0 ? (c + delta) & 0xFFFF : U16(rangeOffsets + 2 * s + rangeOffset + 2 * (c - start));
                    if (glyph != 0)
                        covered.Add(c);
                }
            }

            return covered;
        }
    }
}
