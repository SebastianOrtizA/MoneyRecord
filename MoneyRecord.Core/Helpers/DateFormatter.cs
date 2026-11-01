using System.Globalization;
using System.Text.RegularExpressions;

namespace MoneyRecord.Helpers
{
    public enum DateStyle
    {
        /// <summary>October 11, 2026 / 11 de octubre de 2026</summary>
        Long,
        /// <summary>Oct 11, 2026 / 11 de oct de 2026</summary>
        Medium,
        /// <summary>Oct 11 / 11 de oct</summary>
        MonthDay,
        /// <summary>Oct / oct (a month on a chart axis)</summary>
        Month,
        /// <summary>Oct 2026 / oct de 2026</summary>
        MonthYear
    }

    /// <summary>
    /// Date patterns derived from the culture's own long-date and month-day patterns, so every language gets its
    /// natural word order instead of a fixed English "MMM dd, yyyy".
    /// </summary>
    public static partial class DateFormatter
    {
        /// <summary>The <see cref="DateStyle.Medium"/> pattern of the current culture (for DatePicker.Format).</summary>
        public static string MediumPattern => GetPattern(DateStyle.Medium);

        public static string Format(DateTime date, DateStyle style, CultureInfo? culture = null)
        {
            culture ??= CultureInfo.CurrentCulture;
            return date.ToString(GetPattern(style, culture), culture);
        }

        /// <summary>The whole month name with the year, capitalized: "October 2026" / "Octubre de 2026" (a month heading).</summary>
        public static string FormatMonthYear(DateTime date, CultureInfo? culture = null)
        {
            culture ??= CultureInfo.CurrentCulture;
            var text = date.ToString(culture.DateTimeFormat.YearMonthPattern, culture);
            return text.Length == 0 ? text : char.ToUpper(text[0], culture) + text[1..];
        }

        /// <summary>
        /// "Oct 1 – Oct 31, 2026" (<see cref="DateStyle.Medium"/>; the start leaves its year out when both are in the same
        /// year, so the text stays short), or one date when both are the same day.
        /// </summary>
        public static string FormatRange(DateTime start, DateTime end, CultureInfo? culture = null)
        {
            if (start.Date == end.Date)
                return Format(start, DateStyle.Medium, culture);

            var first = Format(start, start.Year == end.Year ? DateStyle.MonthDay : DateStyle.Medium, culture);
            return $"{first} – {Format(end, DateStyle.Medium, culture)}";
        }

        public static string GetPattern(DateStyle style, CultureInfo? culture = null)
        {
            var format = (culture ?? CultureInfo.CurrentCulture).DateTimeFormat;
            return style switch
            {
                DateStyle.Long => WithoutWeekday(format.LongDatePattern),
                DateStyle.Medium => AbbreviateMonth(WithoutWeekday(format.LongDatePattern)),
                DateStyle.Month => "MMM",
                DateStyle.MonthYear => AbbreviateMonth(format.YearMonthPattern),
                _ => AbbreviateMonth(format.MonthDayPattern)
            };
        }

        // "dddd, MMMM d, yyyy" → "MMMM d, yyyy"; "yyyy年M月d日dddd" → "yyyy年M月d日"
        private static string WithoutWeekday(string pattern) =>
            WeekdayRegex().Replace(pattern, " ").Trim();

        private static string AbbreviateMonth(string pattern) =>
            pattern.Replace("MMMM", "MMM", StringComparison.Ordinal);

        [GeneratedRegex(@"\s*,?\s*dddd\s*,?\s*")]
        private static partial Regex WeekdayRegex();
    }
}
