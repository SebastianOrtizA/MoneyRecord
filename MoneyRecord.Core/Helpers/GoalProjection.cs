using MoneyRecord.Models;

namespace MoneyRecord.Helpers
{
    /// <summary>
    /// A goal's outlook: the amount still needed each month to reach it by its date, and when the recent pace (the
    /// linked accounts' average monthly change over the last <see cref="PaceMonths"/> months) would reach it.
    /// </summary>
    public static class GoalProjection
    {
        /// <summary>How far back the pace looks.</summary>
        public const int PaceMonths = 3;

        /// <summary>Less history than this gives no pace: a few days say nothing about a month.</summary>
        public const int MinimumPaceDays = 28;

        private const double DaysPerMonth = 365.2425 / 12;

        /// <summary>Projections further out than this are "not in sight" (and can't overflow a DateTime).</summary>
        private const double MaxProjectedMonths = 100 * 12;

        /// <summary>
        /// Where the pace window starts: <see cref="PaceMonths"/> months before <paramref name="today"/>, or later when the
        /// first linked account was created later.
        /// </summary>
        public static DateTime PaceStart(DateTime today, DateTime? firstAccountCreated)
        {
            var start = today.Date.AddMonths(-PaceMonths);
            return firstAccountCreated?.Date > start ? firstAccountCreated.Value.Date : start;
        }

        /// <summary>The average change per month since <paramref name="start"/>; null with less than <see cref="MinimumPaceDays"/>.</summary>
        public static decimal? MonthlyPace(decimal change, DateTime start, DateTime today)
        {
            var days = (today.Date - start.Date).TotalDays;
            return days < MinimumPaceDays ? null : Math.Round(change / (decimal)(days / DaysPerMonth), 2);
        }

        /// <summary>
        /// What must be saved each month from <paramref name="today"/> to have <paramref name="remaining"/> by
        /// <paramref name="targetDate"/>; within the last month, all of it.
        /// </summary>
        public static decimal NeededPerMonth(decimal remaining, DateTime targetDate, DateTime today)
        {
            var months = Math.Max(1, (targetDate.Date - today.Date).TotalDays / DaysPerMonth);
            return Math.Round(remaining / (decimal)months, 2, MidpointRounding.AwayFromZero);
        }

        /// <summary>When <paramref name="remaining"/> is saved at <paramref name="monthlyPace"/>; null when that never happens.</summary>
        public static DateTime? ProjectedDate(decimal remaining, decimal? monthlyPace, DateTime today)
        {
            if (monthlyPace is not > 0)
                return null;

            var months = (double)(remaining / monthlyPace.Value);
            return months > MaxProjectedMonths ? null : today.Date.AddDays(Math.Ceiling(months * DaysPerMonth));
        }

        /// <summary>
        /// Reached once saved; with a target date, on track when the pace gets there in time and behind otherwise (also
        /// once the date has passed); unknown (<see cref="GoalStatus.None"/>) without a date or without enough history.
        /// </summary>
        public static GoalStatus Status(decimal remaining, DateTime? targetDate, decimal? monthlyPace, DateTime? projected,
            DateTime today)
        {
            if (remaining <= 0)
                return GoalStatus.Reached;
            if (targetDate is not { } target)
                return GoalStatus.None;
            if (target.Date < today.Date)
                return GoalStatus.Behind;
            if (monthlyPace == null)
                return GoalStatus.None;
            return projected is { } date && date <= target.Date ? GoalStatus.OnTrack : GoalStatus.Behind;
        }
    }
}
