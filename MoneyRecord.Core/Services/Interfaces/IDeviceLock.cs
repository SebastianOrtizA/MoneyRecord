namespace MoneyRecord.Services.Interfaces
{
    /// <summary>
    /// The phone's own lock (fingerprint, face, or its PIN / pattern / password) for the app lock. Android only for now;
    /// elsewhere Settings hides the app lock.
    /// </summary>
    public interface IDeviceLock
    {
        bool IsSupported { get; }

        /// <summary>The phone has a screen lock (or biometrics) the app can ask for.</summary>
        bool IsAvailable { get; }

        /// <summary>Asks for the phone's lock; true when the user passed it, false when cancelled or unavailable.</summary>
        Task<bool> AuthenticateAsync(string title, string subtitle);

        /// <summary>Hides the app's content in the recent apps screen (while the lock is on).</summary>
        void HideContentInRecents(bool hide);
    }
}
