namespace MoneyRecord.Models
{
    /// <summary>What opens the app lock screen.</summary>
    public enum LockMethod
    {
        /// <summary>The phone's own lock (fingerprint, face, screen lock).</summary>
        DeviceLock,

        /// <summary>The app's own PIN.</summary>
        Pin,

        /// <summary>Either of them: the PIN pad with a "Use phone lock" button.</summary>
        DeviceLockOrPin
    }
}
