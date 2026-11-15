using MoneyRecord.Models;

namespace MoneyRecord.Services.Sync
{
    /// <summary>
    /// Hybrid logical clock: wall clock milliseconds in the high bits, a counter for the same millisecond, kept in
    /// <see cref="SyncState.LastClock"/>. It never goes back (a phone with a wrong clock cannot make later edits look older) and
    /// moves past every clock it has seen from another device.
    /// </summary>
    public static class SyncClock
    {
        public static long Tick(SyncState state, TimeProvider time) => Tick(state, time.GetUtcNow().ToUnixTimeMilliseconds());

        /// <param name="wallMs">When the change happened, in milliseconds since 1970.</param>
        public static long Tick(SyncState state, long wallMs)
        {
            var physical = Math.Max(wallMs, state.LastClock >> 16);
            state.LastClock = physical == state.LastClock >> 16 ? state.LastClock + 1 : physical << 16;
            return state.LastClock;
        }

        public static void Observe(SyncState state, long remote)
        {
            if (remote > state.LastClock)
                state.LastClock = remote;
        }

        /// <summary>The later change wins; the same clock is decided by the device id, so every device picks the same one.</summary>
        public static int Compare(long clockA, string deviceA, long clockB, string deviceB) =>
            clockA != clockB ? clockA.CompareTo(clockB) : string.CompareOrdinal(deviceA, deviceB);
    }
}
