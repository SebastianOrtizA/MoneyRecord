using MoneyRecord.Models;

namespace MoneyRecord.Helpers
{
    /// <summary>Which days of a challenge the game page shows.</summary>
    public enum ChallengeView
    {
        /// <summary>30 days from the first day not done (the last 30 when all are done).</summary>
        Next30,
        ThisMonth,
        LastMonth,
        NextMonth,
        /// <summary>The goal's last 30 days.</summary>
        GoalEnd,
        DateRange
    }

    public static class ChallengeDayFilter
    {
        public const int WindowDays = 30;

        /// <summary>
        /// The days of <paramref name="days"/> (in order) in <paramref name="view"/>; months are the calendar months around
        /// <paramref name="today"/>, the range is inclusive and either end may be open. <paramref name="remainingOnly"/> then
        /// leaves out the days done.
        /// </summary>
        public static List<ChallengeDayInfo> Select(IReadOnlyList<ChallengeDayInfo> days, ChallengeView view, DateTime today,
            DateTime? from, DateTime? to, bool remainingOnly)
        {
            IEnumerable<ChallengeDayInfo> selected;
            switch (view)
            {
                case ChallengeView.ThisMonth:
                    selected = InMonth(days, today);
                    break;
                case ChallengeView.LastMonth:
                    selected = InMonth(days, today.AddMonths(-1));
                    break;
                case ChallengeView.NextMonth:
                    selected = InMonth(days, today.AddMonths(1));
                    break;
                case ChallengeView.GoalEnd:
                    selected = days.Skip(Math.Max(0, days.Count - WindowDays));
                    break;
                case ChallengeView.DateRange:
                    if (from > to)
                        (from, to) = (to, from);
                    selected = days.Where(d => (from == null || d.Date.Date >= from.Value.Date) && (to == null || d.Date.Date <= to.Value.Date));
                    break;
                default:
                    var first = days.ToList().FindIndex(d => !d.IsDone);
                    selected = first < 0 ? days.Skip(Math.Max(0, days.Count - WindowDays)) : days.Skip(first).Take(WindowDays);
                    break;
            }
            return (remainingOnly ? selected.Where(d => !d.IsDone) : selected).ToList();
        }

        private static IEnumerable<ChallengeDayInfo> InMonth(IReadOnlyList<ChallengeDayInfo> days, DateTime month) =>
            days.Where(d => d.Date.Year == month.Year && d.Date.Month == month.Month);
    }
}
