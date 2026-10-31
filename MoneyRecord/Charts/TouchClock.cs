namespace MoneyRecord.Charts
{
    /// <summary>
    /// When the finger last went down anywhere in the app, and a signal when it lifts (the platform's activity feeds both), so
    /// a tap handler can tell a quick tap from a long press and a long press can end its preview. Never raised on platforms
    /// that don't report touches.
    /// </summary>
    public static class TouchClock
    {
        public static DateTime? LastDownUtc { get; set; }

        /// <summary>The finger lifted (or the touch was cancelled).</summary>
        public static event Action? Released;

        public static void RaiseReleased() => Released?.Invoke();
    }
}
