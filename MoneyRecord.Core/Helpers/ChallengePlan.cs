using MoneyRecord.Models;

namespace MoneyRecord.Helpers
{
    /// <summary>The day amounts of a savings challenge.</summary>
    /// <param name="DayCents">Day 1 first, after rounding.</param>
    /// <param name="MaxUsefulDoublings">How many doublings fit the goal before the first amount drops under half a unit.</param>
    /// <param name="UsesLevels">Doubling with more days than doublings: each level lasts several days.</param>
    public sealed record ChallengePlan(IReadOnlyList<long> DayCents, long TotalCents, int MaxUsefulDoublings, bool UsesLevels)
    {
        private const long Unit = 100;

        /// <summary>
        /// Doubling: a, 2a, 4a, ... with a = goal / (2^n - 1); when n is over the useful doublings k the plan has k levels that
        /// last several days each (the earlier levels take the remainder). Linear: day i saves i times goal / (n(n+1)/2).
        /// Each amount is rounded to the nearest multiple of <paramref name="roundingCents"/> (cents when 0), at least one step.
        /// Equal: the same amount every day, <paramref name="dayAmountCents"/> when given (then <paramref name="days"/> should be
        /// <see cref="DaysFor"/>; the rounding does not apply), else the goal split over the days and rounded up to a step (down
        /// when the days outrun the goal). The last day takes what is left, so the total is the goal, unless the step is too big
        /// for the days (every day is at least one step): the preview shows the difference.
        /// </summary>
        public static ChallengePlan Build(long goalCents, int days, ChallengeMode mode, long roundingCents, long dayAmountCents = 0)
        {
            if (days < 1)
                throw new ArgumentOutOfRangeException(nameof(days));
            var step = Math.Max(roundingCents, 1);
            if (mode == ChallengeMode.Equal)
                return BuildEqual(goalCents, days, step, dayAmountCents);
            var unit = Math.Max(step, Unit);
            // A first amount of half a unit still counts as useful: $1,000 in 10 days starts at 0.98
            var k = Math.Max(1, (int)Math.Floor(Math.Log2(2.0 * goalCents / unit + 1)));

            var raw = new double[days];
            var levels = false;
            if (mode == ChallengeMode.Linear)
            {
                var s = goalCents / (days * (days + 1L) / 2.0);
                for (var i = 0; i < days; i++)
                    raw[i] = (i + 1) * s;
            }
            else if (days <= k)
            {
                var a = goalCents / (Math.Pow(2, days) - 1);
                for (var i = 0; i < days; i++)
                    raw[i] = a * Math.Pow(2, i);
            }
            else
            {
                levels = true;
                var lengths = Enumerable.Range(0, k).Select(l => days / k + (l < days % k ? 1 : 0)).ToArray();
                var a = goalCents / lengths.Select((len, l) => len * Math.Pow(2, l)).Sum();
                var day = 0;
                for (var l = 0; l < k; l++)
                    for (var j = 0; j < lengths[l]; j++)
                        raw[day++] = a * Math.Pow(2, l);
            }

            var amounts = raw.Select(r => Math.Max(step, (long)Math.Round(r / step, MidpointRounding.AwayFromZero) * step)).ToArray();
            return new ChallengePlan(amounts, amounts.Sum(), k, levels);
        }

        /// <summary>Days needed to save the goal at a fixed amount a day (the last one may be smaller).</summary>
        public static int DaysFor(long goalCents, long dayAmountCents) =>
            dayAmountCents <= 0 ? 1 : (int)Math.Min(int.MaxValue, (goalCents + dayAmountCents - 1) / dayAmountCents);

        private static ChallengePlan BuildEqual(long goalCents, int days, long step, long dayAmountCents)
        {
            long each;
            long lastMin;
            if (dayAmountCents > 0)
            {
                // A fixed amount: the rounding does not apply, the last day is what is left
                each = dayAmountCents;
                lastMin = 1;
            }
            else
            {
                // The goal split over the days, each rounded up to a step. When that would leave the last day under one step
                // (the days outrun the goal) round down instead, so the total stays the goal.
                lastMin = step;
                each = ((goalCents + days - 1) / days + step - 1) / step * step;
                if (goalCents - each * (days - 1) < step)
                    each = Math.Max(step, goalCents / days / step * step);
            }
            var amounts = new long[days];
            Array.Fill(amounts, each);
            amounts[^1] = Math.Max(lastMin, goalCents - each * (days - 1));
            return new ChallengePlan(amounts, amounts.Sum(), 1, false);
        }
    }
}
