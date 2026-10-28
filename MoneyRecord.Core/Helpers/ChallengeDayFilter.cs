using System.Globalization;
using MoneyRecord.Models;

namespace MoneyRecord.Helpers
{
    /// <summary>Which days of a challenge the game page shows.</summary>
    public enum ChallengeView
    {
        /// <summary>Every day, from day 1.</summary>
        FromStart,
        /// <summary>The days of one calendar month, laid out as a calendar.</summary>
        Month,
        /// <summary>30 days from the first day not done (the last 30 when all are done).</summary>
        Next30,
        DateRange
    }

    public static class ChallengeDayFilter
    {
        public const int WindowDays = 30;

        /// <summary>
        /// The days of <paramref name="days"/> (in order) in <paramref name="view"/>; the month is <paramref name="month"/>, the
        /// range is inclusive and either end may be open. <paramref name="remainingOnly"/> then leaves out the days done.
        /// <paramref name="startDay"/> (1-based) pins where <see cref="ChallengeView.Next30"/> starts, so the window does not
        /// slide when a day is marked; without it the window starts at the first day not done.
        /// </summary>
        public static List<ChallengeDayInfo> Select(IReadOnlyList<ChallengeDayInfo> days, ChallengeView view, DateTime month,
            DateTime? from, DateTime? to, bool remainingOnly, int? startDay = null)
        {
            IEnumerable<ChallengeDayInfo> selected;
            switch (view)
            {
                case ChallengeView.FromStart:
                    selected = days;
                    break;
                case ChallengeView.Month:
                    selected = InMonth(days, month);
                    break;
                case ChallengeView.DateRange:
                    if (from > to)
                        (from, to) = (to, from);
                    selected = days.Where(d => (from == null || d.Date.Date >= from.Value.Date) && (to == null || d.Date.Date <= to.Value.Date));
                    break;
                default:
                    var first = startDay is { } start ? Math.Clamp(start - 1, 0, Math.Max(0, days.Count - 1)) : FirstPendingIndex(days);
                    selected = first < 0 ? days.Skip(Math.Max(0, days.Count - WindowDays)) : days.Skip(first).Take(WindowDays);
                    break;
            }
            return (remainingOnly ? selected.Where(d => !d.IsDone) : selected).ToList();
        }

        /// <summary>The first and the last month (each as its 1st) the challenge has days in; null for no days.</summary>
        public static (DateTime First, DateTime Last)? MonthSpan(IReadOnlyList<ChallengeDayInfo> days) =>
            days.Count == 0 ? null : (FirstOfMonth(days[0].Date), FirstOfMonth(days[^1].Date));

        /// <summary><paramref name="month"/> (as its 1st) moved into the months the challenge has days in.</summary>
        public static DateTime ClampMonth(IReadOnlyList<ChallengeDayInfo> days, DateTime month)
        {
            var current = FirstOfMonth(month);
            if (MonthSpan(days) is not { } span)
                return current;
            return current < span.First ? span.First : current > span.Last ? span.Last : current;
        }

        /// <summary>
        /// The month as calendar slots, seven to a week: empty ones (null) up to the first day's weekday, and in place of a day
        /// left out by <paramref name="remainingOnly"/>, so every day sits under its weekday. Empty when nothing is shown.
        /// </summary>
        public static List<ChallengeDayInfo?> MonthSlots(IReadOnlyList<ChallengeDayInfo> days, DateTime month, bool remainingOnly,
            DayOfWeek firstDayOfWeek)
        {
            var inMonth = InMonth(days, month).ToList();
            var slots = new List<ChallengeDayInfo?>();
            if (inMonth.Count == 0)
                return slots;

            slots.AddRange(Enumerable.Repeat<ChallengeDayInfo?>(null, ((int)inMonth[0].Date.DayOfWeek - (int)firstDayOfWeek + 7) % 7));
            slots.AddRange(inMonth.Select(d => remainingOnly && d.IsDone ? null : d));
            while (slots.Count > 0 && slots[^1] == null)
                slots.RemoveAt(slots.Count - 1);
            return slots.All(s => s == null) ? [] : slots;
        }

        /// <summary>The seven short weekday names, starting at the culture's first day of the week.</summary>
        public static string[] WeekdayHeaders(CultureInfo culture)
        {
            var format = culture.DateTimeFormat;
            var first = (int)format.FirstDayOfWeek;
            return Enumerable.Range(0, 7).Select(i => format.ShortestDayNames[(first + i) % 7]).ToArray();
        }

        /// <summary>The number of the first day not done, or null when every day is done.</summary>
        public static int? FirstPendingDay(IReadOnlyList<ChallengeDayInfo> days) =>
            days.FirstOrDefault(d => !d.IsDone)?.DayNumber;

        private static int FirstPendingIndex(IReadOnlyList<ChallengeDayInfo> days)
        {
            for (var i = 0; i < days.Count; i++)
                if (!days[i].IsDone)
                    return i;
            return -1;
        }

        private static DateTime FirstOfMonth(DateTime date) => new(date.Year, date.Month, 1);

        private static IEnumerable<ChallengeDayInfo> InMonth(IReadOnlyList<ChallengeDayInfo> days, DateTime month) =>
            days.Where(d => d.Date.Year == month.Year && d.Date.Month == month.Month);
    }
}
