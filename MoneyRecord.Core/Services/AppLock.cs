using MoneyRecord.Services.Interfaces;

namespace MoneyRecord.Services
{
    /// <summary>
    /// When the app lock locks (singleton): at every start, and when the app comes back after being in the background for
    /// at least <see cref="IPrivacySettings.LockAfterMinutes"/>. The app shows the lock screen when it says so.
    /// </summary>
    public sealed class AppLock(IPrivacySettings settings, IDeviceLock deviceLock, TimeProvider clock)
    {
        private DateTimeOffset? _backgroundedAt;

        // Never without a lock the phone can ask for: removing the screen lock while the app lock is on must not lock
        // the user out (the lock works again once the phone has one)
        private bool CanLock => settings.LockEnabled && deviceLock.IsAvailable;

        public bool IsLocked { get; private set; }

        /// <summary>The app started: true when it must show the lock screen.</summary>
        public bool LockOnStart()
        {
            IsLocked = CanLock;
            return IsLocked;
        }

        /// <summary>The app went to the background.</summary>
        public void Backgrounded() => _backgroundedAt ??= clock.GetUtcNow();

        /// <summary>
        /// The app came back: true when it must show the lock screen now. Already locked means the lock screen is up, so
        /// nothing new (asking again here could stack a second prompt on the one that just ended).
        /// </summary>
        public bool LockOnResume()
        {
            var backgroundedAt = _backgroundedAt;
            _backgroundedAt = null;
            if (!CanLock || IsLocked || backgroundedAt == null)
                return false;

            IsLocked = clock.GetUtcNow() - backgroundedAt.Value >= TimeSpan.FromMinutes(settings.LockAfterMinutes);
            return IsLocked;
        }

        /// <summary>The user passed the phone's lock.</summary>
        public void Unlock() => IsLocked = false;
    }
}
