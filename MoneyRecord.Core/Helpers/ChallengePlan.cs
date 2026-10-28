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
        /// Linear with a step (<paramref name="dayAmountCents"/>): day i saves the first day (<paramref name="firstDayCents"/>,
        /// the step when 0) plus (i - 1) steps; <paramref name="days"/> should be <see cref="DaysForLinear"/>, the rounding does not
        /// apply and the last day is what is left. Linear with a first day but no step: the step is what makes the goal.
        /// Equal: the same amount every day, <paramref name="dayAmountCents"/> when given (then <paramref name="days"/> should be
        /// <see cref="DaysFor"/>; the rounding does not apply), else the goal split over the days and rounded up to a step (down
        /// when the days outrun the goal). The last day takes what is left, so the total is the goal, unless the step is too big
        /// for the days (every day is at least one step): the preview shows the difference.
        /// </summary>
        public static ChallengePlan Build(long goalCents, int days, ChallengeMode mode, long roundingCents, long dayAmountCents = 0,
            long firstDayCents = 0)
        {
            if (days < 1)
                throw new ArgumentOutOfRangeException(nameof(days));
            var step = Math.Max(roundingCents, 1);
            if (mode == ChallengeMode.Equal)
                return BuildEqual(goalCents, days, step, dayAmountCents);
            if (mode == ChallengeMode.Linear && (dayAmountCents > 0 || firstDayCents > 0))
                return BuildLinearFrom(goalCents, days, step, dayAmountCents, firstDayCents);
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

        /// <summary>
        /// Days needed when day 1 saves <paramref name="firstDayCents"/> (the step when 0) and each day <paramref name="stepCents"/>
        /// more: the smallest n whose days add up to the goal (the last one may be smaller).
        /// </summary>
        public static int DaysForLinear(long goalCents, long firstDayCents, long stepCents)
        {
            if (stepCents <= 0)
                return 1;
            var first = firstDayCents > 0 ? firstDayCents : stepCents;
            Int128 Sum(long n) => (Int128)n * first + (Int128)stepCents * n * (n - 1) / 2;

            // n²·step/2 + n·(first - step/2) = goal, solved for n and then corrected by exact sums
            var b = first - stepCents / 2.0;
            var estimate = (-b + Math.Sqrt(b * b + 2.0 * stepCents * goalCents)) / stepCents;
            var n = (long)Math.Clamp(Math.Ceiling(estimate), 1, MaxDaysSearched);
            while (n > 1 && Sum(n - 1) >= goalCents)
                n--;
            while (n < MaxDaysSearched && Sum(n) < goalCents)
                n++;
            return (int)n;
        }

        private const long MaxDaysSearched = 10_000_000;

        /// <summary>Whether <paramref name="days"/> days of at least the first day already pass the goal, so no step fits.</summary>
        public static bool FirstDayTooHigh(long goalCents, int days, long firstDayCents) =>
            days > 1 && firstDayCents > 0 && (Int128)firstDayCents * days > goalCents;

        private static ChallengePlan BuildLinearFrom(long goalCents, int days, long rounding, long stepCents, long firstDayCents)
        {
            var amounts = new long[days];
            if (stepCents > 0)
            {
                // A fixed step: the rounding does not apply, the last day is what is left
                var first = firstDayCents > 0 ? firstDayCents : stepCents;
                long sum = 0;
                for (var i = 0; i < days - 1; i++)
                    sum += amounts[i] = first + i * stepCents;
                amounts[^1] = days == 1 ? goalCents : Math.Max(1, goalCents - sum);
                return new ChallengePlan(amounts, amounts.Sum(), 1, false);
            }

            // A first day and the days: the step that makes the goal, rounded like every Linear plan
            var stepRaw = days == 1 ? 0.0 : Math.Max(0.0, (goalCents - (double)days * firstDayCents) / (days * (days - 1L) / 2.0));
            for (var i = 0; i < days; i++)
            {
                var raw = days == 1 ? goalCents : firstDayCents + i * stepRaw;
                amounts[i] = Math.Max(rounding, (long)Math.Round(raw / rounding, MidpointRounding.AwayFromZero) * rounding);
            }
            return new ChallengePlan(amounts, amounts.Sum(), 1, false);
        }

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
