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
        Month
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

        public static string GetPattern(DateStyle style, CultureInfo? culture = null)
        {
            var format = (culture ?? CultureInfo.CurrentCulture).DateTimeFormat;
            return style switch
            {
                DateStyle.Long => WithoutWeekday(format.LongDatePattern),
                DateStyle.Medium => AbbreviateMonth(WithoutWeekday(format.LongDatePattern)),
                DateStyle.Month => "MMM",
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
