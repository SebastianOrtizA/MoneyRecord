namespace MoneyRecord.Helpers
{
    /// <summary>
    /// The timing rules for a press on a chart: a press held past <see cref="HoldThreshold"/> only inspects, so releasing it is
    /// not a tap; and a hold ends a moment after release unless a new press began in the meantime.
    /// </summary>
    public static class PressTiming
    {
        /// <summary>Android's long-press time.</summary>
        public static readonly TimeSpan HoldThreshold = TimeSpan.FromMilliseconds(400);

        /// <summary>True when the finger went down earlier than <see cref="HoldThreshold"/> before <paramref name="now"/>.</summary>
        public static bool IsHold(DateTime? downUtc, DateTime now) => downUtc is { } down && now - down > HoldThreshold;

        /// <summary>Whether a hold that ended at <paramref name="releasedAt"/> may be cleared: not once a newer press began.</summary>
        public static bool MayClearAfterRelease(DateTime? lastDownUtc, DateTime releasedAt) =>
            lastDownUtc is not { } down || down <= releasedAt;
    }
}
