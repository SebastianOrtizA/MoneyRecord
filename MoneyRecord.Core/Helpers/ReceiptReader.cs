using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MoneyRecord.Models;

namespace MoneyRecord.Helpers
{
    /// <summary>
    /// Finds a receipt's total and date in its OCR text. Lines side by side are joined into rows first ("TOTAL" on the left,
    /// "12,50" on the right). The total is the last amount on the row that names it ("Total a pagar" before a plain
    /// "Total"), never on a subtotal, tax, discount, cash handed over or change row; a receipt without one gets its largest
    /// price. The date is the first one that is neither in the future nor more than <see cref="MaxAgeYears"/> years old.
    /// </summary>
    public static partial class ReceiptReader
    {
        public const int MaxAgeYears = 5;

        // A whole number on a total row ("TOTAL 25000" in a currency without cents); longer ones are references or phones
        private const int MaxIntegerDigits = 9;

        private static readonly CultureInfo[] MonthCultures = [CultureInfo.InvariantCulture, CultureInfo.GetCultureInfo("es-ES")];

        public static ReceiptReading Read(IEnumerable<ReceiptLine> lines, DateTime today, CultureInfo? culture = null) =>
            Read(Rows(lines), today, culture);

        public static ReceiptReading Read(IReadOnlyList<string> rows, DateTime today, CultureInfo? culture = null)
        {
            culture ??= CultureInfo.CurrentCulture;
            DateTime? date = null;
            var cleaned = new List<string>(rows.Count);
            foreach (var row in rows)
            {
                // Dates and times come out first, so their digits aren't read as amounts
                var rest = TakeDates(row, today.Date, culture, ref date);
                cleaned.Add(TimeRegex().Replace(rest, " "));
            }
            return new ReceiptReading(FindTotal(cleaned), date);
        }

        /// <summary>
        /// The lines as rows, top to bottom, each one's lines left to right: a line joins the row above when its middle is
        /// within that row's first line.
        /// </summary>
        public static List<string> Rows(IEnumerable<ReceiptLine> lines)
        {
            var rows = new List<List<ReceiptLine>>();
            foreach (var line in lines.Where(l => !string.IsNullOrWhiteSpace(l.Text)).OrderBy(l => l.CenterY))
            {
                if (rows.Count > 0 && line.CenterY <= rows[^1][0].Bottom)
                    rows[^1].Add(line);
                else
                    rows.Add([line]);
            }
            return rows.Select(r => string.Join("  ", r.OrderBy(l => l.Left).Select(l => l.Text.Trim()))).ToList();
        }

        private static decimal? FindTotal(List<string> rows)
        {
            var bestRank = 0;
            decimal? best = null;
            decimal? largest = null;
            foreach (var row in rows)
            {
                var text = Normalize(row);
                if (IsExcluded(text))
                    continue;

                var amounts = Amounts(row);
                var rank = StrongTotalRegex().IsMatch(text) ? 2 : TotalRegex().IsMatch(text) ? 1 : 0;
                if (rank > 0 && amounts.Count > 0 && amounts[^1].Value is var amount and > 0
                    && (rank > bestRank || rank == bestRank && amount > best))
                {
                    bestRank = rank;
                    best = amount;
                }
                // Without a total row: the largest price (written with a separator; plain numbers are quantities, codes…)
                foreach (var (value, separated) in amounts)
                {
                    if (separated && value > (largest ?? 0))
                        largest = value;
                }
            }
            return best ?? largest;
        }

        private static bool IsExcluded(string text) =>
            ExcludedRegex().IsMatch(text) || TaxRegex().IsMatch(text) && !IncludedRegex().IsMatch(text);

        /// <summary>
        /// The row's amounts, left to right: "12,50", "1.234,56", "1,234.56", "$25.000" (a group of three digits after the last
        /// separator is thousands, one or two are cents). Negative ones (discounts) and percentages are left out.
        /// </summary>
        private static List<(decimal Value, bool Separated)> Amounts(string row)
        {
            var amounts = new List<(decimal, bool)>();
            foreach (Match match in AmountRegex().Matches(row))
            {
                if (match.Index > 0 && row[match.Index - 1] is '-' or '−')
                    continue;

                var token = match.Value;
                var last = token.LastIndexOfAny(['.', ',']);
                var hasCents = last >= 0 && token.Length - last - 1 is 1 or 2;
                var whole = new string((hasCents ? token[..last] : token).Where(char.IsDigit).ToArray());
                var cents = hasCents ? token[(last + 1)..] : "0";
                var separated = whole.Length != token.Length;
                if (!separated && whole.Length > MaxIntegerDigits)
                    continue;
                if (decimal.TryParse($"{whole}.{cents}", NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
                    amounts.Add((value, separated));
            }
            return amounts;
        }

        /// <summary>Removes the row's dates; the first one in range (if none was found yet) goes into <paramref name="date"/>.</summary>
        private static string TakeDates(string row, DateTime today, CultureInfo culture, ref DateTime? date)
        {
            var found = date;
            var monthFirst = culture.DateTimeFormat.ShortDatePattern.TrimStart().StartsWith('M');

            row = NumericDateRegex().Replace(row, m =>
            {
                int a = int.Parse(m.Groups["a"].Value), b = int.Parse(m.Groups["b"].Value), c = int.Parse(m.Groups["c"].Value);
                // 2026-10-14; else the year is last and 05/04 is day-first or month-first (the culture's order first)
                (int Year, int Month, int Day)[] options = m.Groups["a"].Length == 4
                    ? [(a, b, c)]
                    : monthFirst ? [(Year(c), a, b), (Year(c), b, a)] : [(Year(c), b, a), (Year(c), a, b)];
                found ??= options.Select(o => Valid(o.Year, o.Month, o.Day, today)).FirstOrDefault(d => d != null);
                return " ";
            });
            row = DayMonthYearRegex().Replace(row, m =>
            {
                if (Month(m.Groups["month"].Value) is not { } month)
                    return m.Value;
                found ??= Valid(Year(int.Parse(m.Groups["year"].Value)), month, int.Parse(m.Groups["day"].Value), today);
                return " ";
            });
            row = MonthDayYearRegex().Replace(row, m =>
            {
                if (Month(m.Groups["month"].Value) is not { } month)
                    return m.Value;
                found ??= Valid(int.Parse(m.Groups["year"].Value), month, int.Parse(m.Groups["day"].Value), today);
                return " ";
            });

            date = found;
            return row;

            int? Month(string name)
            {
                var key = Normalize(name).TrimEnd('.');
                if (key.Length < 3)
                    return null;
                foreach (var format in MonthCultures.Prepend(culture).Select(c => c.DateTimeFormat))
                {
                    for (var i = 0; i < 12; i++)
                    {
                        // "oct", "oct.", "octubre", "sept": the abbreviation, or the start of the full name
                        if (Normalize(format.AbbreviatedMonthNames[i]).TrimEnd('.') == key || Normalize(format.MonthNames[i]).StartsWith(key))
                            return i + 1;
                    }
                }
                return null;
            }
        }

        private static int Year(int year) => year < 100 ? 2000 + year : year;

        private static DateTime? Valid(int year, int month, int day, DateTime today)
        {
            if (month is < 1 or > 12 || year is < 1 or > 9999 || day < 1 || day > DateTime.DaysInMonth(year, month))
                return null;
            var date = new DateTime(year, month, day);
            return date <= today && date >= today.AddYears(-MaxAgeYears) ? date : null;
        }

        /// <summary>Lower case without accents: "TOTAL A PAGAR", "Total a Pagar" and "total à pagar" read the same.</summary>
        private static string Normalize(string text)
        {
            var decomposed = text.ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var plain = new StringBuilder(decomposed.Length);
            foreach (var c in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    plain.Append(c);
            }
            return plain.ToString();
        }

        [GeneratedRegex(@"\b(total a pagar|a pagar|amount due|balance due|total due|grand total|total general|total factura|total venta|total compra)\b")]
        private static partial Regex StrongTotalRegex();

        [GeneratedRegex(@"\b(total|importe|amount|suma|monto)\b")]
        private static partial Regex TotalRegex();

        [GeneratedRegex(@"\b(sub\s*-?\s*total|descuentos?|dto|discount|ahorro|savings?|cambio|change|vuelto|vueltas|devuelta|efectivo|cash|recibido|entregado|tendered|items?|articulos?|unidades|uds|cantidad|qty|puntos|points|propina|tip)\b")]
        private static partial Regex ExcludedRegex();

        [GeneratedRegex(@"\b(iva|tax|taxes|vat|impuestos?|igic|base)\b")]
        private static partial Regex TaxRegex();

        // "Total (IVA incluido)", "Total incl. tax": a total row that mentions the tax
        [GeneratedRegex(@"\b(incl|incluido|incluidos|included|inc)\b")]
        private static partial Regex IncludedRegex();

        [GeneratedRegex(@"(?<![\p{L}\d.,])(?:\d{1,3}(?:[.,'’  ]\d{3})+(?:[.,]\d{1,2})?|\d+(?:[.,]\d{1,2})?)(?![\p{L}\d%]|[.,]\d)")]
        private static partial Regex AmountRegex();

        [GeneratedRegex(@"(?<![\d/.\-])(?<a>\d{1,4})(?<sep>[/.\-])(?<b>\d{1,2})\k<sep>(?<c>\d{4}|\d{2})(?!\d)")]
        private static partial Regex NumericDateRegex();

        // "14 oct 2026", "14-OCT-26", "14 de octubre de 2026"
        [GeneratedRegex(@"(?<![\p{L}\d])(?<day>\d{1,2})(?:\s*de\s+|[\s\-/.]*)(?<month>\p{L}{3,10}\.?)(?:\s+de\s+|[\s\-/.,]+)(?<year>\d{4}|\d{2})(?!\d)", RegexOptions.IgnoreCase)]
        private static partial Regex DayMonthYearRegex();

        // "Oct 14, 2026"
        [GeneratedRegex(@"(?<![\p{L}\d])(?<month>\p{L}{3,10}\.?)\s+(?<day>\d{1,2}),?\s+(?<year>\d{4})(?!\d)", RegexOptions.IgnoreCase)]
        private static partial Regex MonthDayYearRegex();

        [GeneratedRegex(@"(?<!\d)\d{1,2}:\d{2}(?::\d{2})?(?:\s?[ap]\.?\s?m\.?)?", RegexOptions.IgnoreCase)]
        private static partial Regex TimeRegex();
    }
}
