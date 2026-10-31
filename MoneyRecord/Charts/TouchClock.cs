namespace MoneyRecord.Charts
{
    /// <summary>
    /// When the finger last went down anywhere in the app (the platform's activity feeds it), so a tap handler can tell a
    /// quick tap from a long press that ended in a release. Never set on platforms that don't report it.
    /// </summary>
    public static class TouchClock
    {
        public static DateTime? LastDownUtc { get; set; }
    }
}
