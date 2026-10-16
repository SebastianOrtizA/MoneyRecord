namespace MoneyRecord.Helpers
{
    /// <summary>The money of a split payment: what is left to assign, and a total shared out in proportion to the parts.</summary>
    public static class SplitAllocation
    {
        /// <summary>The total minus the parts entered so far (empty parts count as 0); negative = the parts are over the total.</summary>
        public static long Remaining(long totalCents, IEnumerable<long?> partCents) =>
            totalCents - partCents.Sum(c => c ?? 0);

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
}
