namespace MoneyRecord.Helpers
{
    /// <summary>The money of a split payment: what is left to assign, and a total shared out in proportion to the parts.</summary>
    public static class SplitAllocation
    {
        /// <summary>The total minus the parts entered so far (empty parts count as 0); negative = the parts are over the total.</summary>
        public static long Remaining(long totalCents, IEnumerable<long?> partCents) =>
            totalCents - partCents.Sum(c => c ?? 0);

        /// <summary>
        /// The parts of a split as saved: one part left empty (null) takes what is left of the total; then every part needs a
        /// positive amount and together they must make the total. <see cref="SplitCompletion.Cents"/> is null when a
        /// <see cref="SplitProblem"/> says what to fix.
        /// </summary>
        public static SplitCompletion Complete(long totalCents, IReadOnlyList<long?> partCents)
        {
            var cents = partCents.ToList();
            if (cents.Count(c => c == null) == 1)
            {
                var rest = Remaining(totalCents, cents);
                // The others already make the total (nothing left for it) or more
                if (rest <= 0)
                    return new(null, rest < 0 ? SplitProblem.DoesNotAddUp : SplitProblem.MissingAmount);
                cents[cents.IndexOf(null)] = rest;
            }

            if (cents.Any(c => c is null or <= 0))
                return new(null, SplitProblem.MissingAmount);
            if (cents.Sum() != totalCents)
                return new(null, SplitProblem.DoesNotAddUp);
            return new(cents.Select(c => c!.Value).ToArray(), SplitProblem.None);
        }

        /// <summary>
        /// <paramref name="totalCents"/> shared in proportion to <paramref name="weights"/> (all positive), adding up exactly:
        /// each share is rounded down and the leftover cents go to the parts with the largest fractions (ties: earlier part).
        /// The user's own "Amount in USD" for a split payment, shared out over its parts.
        /// </summary>
        public static long[] Share(long totalCents, IReadOnlyList<long> weights)
        {
            if (weights.Count == 0)
                return [];
            if (weights.Any(w => w <= 0))
                throw new ArgumentException("Every part needs a positive amount.", nameof(weights));

            var sum = weights.Sum();
            var shares = new long[weights.Count];
            var fractions = new (decimal Fraction, int Index)[weights.Count];
            for (var i = 0; i < weights.Count; i++)
            {
                var exact = (decimal)totalCents * weights[i] / sum;
                shares[i] = (long)Math.Floor(exact);
                fractions[i] = (exact - shares[i], i);
            }

            var leftover = totalCents - shares.Sum();
            foreach (var (_, index) in fractions.OrderByDescending(f => f.Fraction).ThenBy(f => f.Index).Take((int)leftover))
                shares[index]++;
            return shares;
        }
    }

    /// <summary>Why a split can't be saved yet.</summary>
    public enum SplitProblem
    {
        None,
        /// <summary>A part has no amount (or nothing is left for the one empty part).</summary>
        MissingAmount,
        /// <summary>The parts don't make the total.</summary>
        DoesNotAddUp
    }

    /// <summary>The completed parts in cents (null with a problem), see <see cref="SplitAllocation.Complete"/>.</summary>
    public sealed record SplitCompletion(long[]? Cents, SplitProblem Problem);
}
