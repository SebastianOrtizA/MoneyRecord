using System.Collections.Concurrent;
using System.Globalization;

namespace MoneyRecord.Helpers
{
    /// <summary>
    /// The date layouts bank statements use, with an optional time after the date (dropped: an entry has a day).
    /// <see cref="Detect"/> finds the ones that read a whole column; 05/04/2026 fits both day-first and month-first, so
    /// the import screen offers the choice, the current culture's order first.
    /// </summary>
    public sealed class StatementDate
    {
        private static readonly string[] TimeSuffixes = ["", " HH:mm", " HH:mm:ss", " H:mm", " H:mm:ss", "'T'HH:mm", "'T'HH:mm:ss",
            "'T'HH:mm:ss.FFFFFFF", " h:mm tt", " h:mm:ss tt"];

        private static readonly ConcurrentDictionary<string, CultureInfo> DotlessCultures = new();

        private readonly string[] _patterns;
        private readonly bool _hasMonthName;

        private StatementDate(string pattern)
        {
            Pattern = pattern;
            _patterns = TimeSuffixes.Select(s => pattern + s).ToArray();
            _hasMonthName = pattern.Contains("MMM");
        }

        /// <summary>The date part, e.g. "dd/MM/yyyy"; shown in the import screen's format picker.</summary>
        public string Pattern { get; }

        public static IReadOnlyList<StatementDate> Formats { get; } =
        [
            new("yyyy-MM-dd"), new("yyyy/MM/dd"), new("yyyyMMdd"), new("yyyy.MM.dd"),
            new("d/M/yyyy"), new("M/d/yyyy"), new("d-M-yyyy"), new("M-d-yyyy"), new("d.M.yyyy"),
            new("d/M/yy"), new("M/d/yy"), new("d-M-yy"), new("d.M.yy"),
            new("d MMM yyyy"), new("d-MMM-yyyy"), new("d/MMM/yyyy"), new("MMM d, yyyy"), new("d-MMM-yy"),
        ];

        /// <summary>Reads <paramref name="text"/> as this layout, with month names in the current or English culture.</summary>
        public bool TryParse(string text, out DateTime date)
        {
            var value = text.Trim().TrimEnd('Z');
            foreach (var culture in new[] { CultureInfo.CurrentCulture, CultureInfo.InvariantCulture })
            {
                if (DateTime.TryParseExact(value, _patterns, culture, DateTimeStyles.AllowWhiteSpaces, out var parsed)
                    // "sept." / "Sept": .NET's abbreviated names may carry a dot the bank leaves out, or the other way round
                    || _hasMonthName && DateTime.TryParseExact(value.Replace(".", ""), _patterns, WithoutDots(culture),
                        DateTimeStyles.AllowWhiteSpaces, out parsed))
                {
                    date = parsed.Date;
                    return true;
                }
            }
            date = default;
            return false;
        }

        /// <summary>
        /// The layouts that read most of the column (a totals line may not be a date), the ones reading the most values
        /// first, then the shortest span of dates, then the current culture's day/month order; empty when none does.
        /// </summary>
        public static List<StatementDate> Detect(IEnumerable<string> values)
        {
            var sample = values.Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
            if (sample.Count == 0)
                return [];

            var monthFirst = CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern.TrimStart().StartsWith('M');
            return Formats
                .Select(f => (Format: f, Dates: sample.Select(v => f.TryParse(v, out var d) ? d : (DateTime?)null)
                    .OfType<DateTime>().ToList()))
                .Where(f => StatementAmount.IsMost(f.Dates.Count, sample.Count))
                .OrderByDescending(f => f.Dates.Count)
                // A statement covers a short period: 01/10 … 10/10 is ten days of October, not January to October
                .ThenBy(f => f.Dates.Max() - f.Dates.Min())
                .ThenBy(f => f.Format.Pattern.StartsWith('y') || f.Format.Pattern.StartsWith('M') == monthFirst ? 0 : 1)
                .Select(f => f.Format)
                .ToList();
        }

        public override string ToString() => Pattern;

        private static CultureInfo WithoutDots(CultureInfo culture) => DotlessCultures.GetOrAdd(culture.Name, _ =>
        {
            var copy = (CultureInfo)culture.Clone();
            var format = copy.DateTimeFormat;
            format.AbbreviatedMonthNames = format.AbbreviatedMonthNames.Select(n => n.Replace(".", "")).ToArray();
            format.AbbreviatedMonthGenitiveNames = format.AbbreviatedMonthGenitiveNames.Select(n => n.Replace(".", "")).ToArray();
            return copy;
        });
    }
}
