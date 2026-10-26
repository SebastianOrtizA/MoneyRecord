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
        /// </summary>
        public static ChallengePlan Build(long goalCents, int days, ChallengeMode mode, long roundingCents)
        {
            if (days < 1)
                throw new ArgumentOutOfRangeException(nameof(days));
            var step = Math.Max(roundingCents, 1);
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
    }
}
