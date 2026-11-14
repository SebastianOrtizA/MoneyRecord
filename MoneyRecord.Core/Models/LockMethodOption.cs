using MoneyRecord.Resources.Strings;

namespace MoneyRecord.Models
{
    /// <summary>A Settings choice for what opens the lock screen, in the current language.</summary>
    public sealed record LockMethodOption(LockMethod Method, string Name)
    {
        /// <param name="deviceLockAvailable">The phone has a lock to offer; without one only the PIN is a way in.</param>
        public static IReadOnlyList<LockMethodOption> All(bool deviceLockAvailable) => deviceLockAvailable
            ? [new(LockMethod.DeviceLock, AppResources.LockMethodDevice), new(LockMethod.Pin, AppResources.LockMethodPin),
                new(LockMethod.DeviceLockOrPin, AppResources.LockMethodBoth)]
            : [new(LockMethod.Pin, AppResources.LockMethodPin)];
    }
}
